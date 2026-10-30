using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace TiffanyGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class TylerController : MonoBehaviour
    {
        public Transform player;
        public WitchController witch;
        public Transform nextMemory;
        public GameObject figure;
        public Transform head;
        public AudioSource voice;
        public Text reactionIcon;
        public AudioClip barkClip;
        public AudioClip whineClip;
        public AudioClip growlClip;
        public Animator animator;
        public Light shareLight;
        public PlayerSoul soul;
        public ShareMode shareMode = ShareMode.LightOnly;
        public float repathThreshold = 0.5f;
        public float nearPlayerRadius = 6f;

        public TylerReaction Reaction { get; private set; }
        public bool SharingLight { get; private set; }
        public bool NearPlayer
        {
            get
            {
                if (!isActiveAndEnabled || player == null) return false;
                Vector3 offset = player.position - transform.position;
                offset.y = 0f;
                return offset.sqrMagnitude < nearPlayerRadius * nearPlayerRadius;
            }
        }
        public float Speed
        {
            get
            {
                if (agent == null || !agent.enabled || !agent.isOnNavMesh) return 0f;
                Vector3 velocity = agent.velocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        private Tyler brain;
        private readonly TylerLightShare lightShare = new TylerLightShare();
        private readonly TylerCatchUp catchUp = new TylerCatchUp();
        private readonly TylerIcon icon = new TylerIcon();
        private Camera view;
        private bool heardMimic, heardForest;
        private Vector3? forestPoint;
        private NavMeshAgent agent;
        private Vector3 previousPlayerPosition;
        private Vector3 lastDestination;
        private bool hasDestination;
        private bool warnedMissingNavMesh;
        private TylerPose lastPose;
        private bool hasPose;
        private float idleSeconds;
        private int speedParameterHash;
        private int idleHash;
        private int walkHash;
        private int sitHash;
        private int whineHash;
        private int growlHash;
        private int barkHash;
        private int lastAnimationStateHash;
        private bool hasAnimationState;

        private void Awake()
        {
            brain = new Tyler();
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.acceleration = 30f;
            agent.angularSpeed = 720f;
            agent.autoBraking = false;
            speedParameterHash = Animator.StringToHash(TylerAnimation.SpeedParameter);
            idleHash = Animator.StringToHash(TylerAnimation.Idle);
            walkHash = Animator.StringToHash(TylerAnimation.Walk);
            sitHash = Animator.StringToHash(TylerAnimation.Sit);
            whineHash = Animator.StringToHash(TylerAnimation.Whine);
            growlHash = Animator.StringToHash(TylerAnimation.Growl);
            barkHash = Animator.StringToHash(TylerAnimation.Bark);
        }

        private void OnEnable()
        {
            hasAnimationState = false;
            catchUp.Reset();
            icon.Reset();
            if (player != null) previousPlayerPosition = player.position;
        }

        private void OnDisable()
        {
            if (agent != null) agent.enabled = false;
            catchUp.Reset();
            icon.Reset();
            if (reactionIcon != null) reactionIcon.enabled = false;
            lightShare.Tick(0f, shareMode, false, false, false, 0f, out _);
            SetSharingLight(false);
        }

        private void Start()
        {
            view = Camera.main;
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }
            if (player != null) previousPlayerPosition = player.position;
        }

        public void Appear(Vector3 near)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            Vector3 forward = player != null ? player.forward : Vector3.forward;
            transform.position = near - forward * TylerMotion.FollowDistance;
            gameObject.SetActive(true);
            EnableAgent();
            if (voice != null && barkClip != null)
            {
                voice.PlayOneShot(barkClip);
                icon.Bark();
            }
        }

        public void HearMimic()
        {
            if (isActiveAndEnabled) heardMimic = true;
        }

        public void HearForest(Vector3 point)
        {
            if (!isActiveAndEnabled) return;
            heardForest = true;
            forestPoint = point;
        }

        private void Update()
        {
            bool sharing = lightShare.Tick(Time.deltaTime, shareMode,
                soul != null && soul.Energy.IsEmpty, soul != null && NearPlayer,
                soul != null && soul.RechargingAtCandle, soul != null ? soul.Energy.Current : 0f,
                out float recharge);
            if (recharge > 0f) soul.Energy.Recharge(recharge / SoulCosts.RechargePerSecond);
            SetSharingLight(sharing);
            float speed = Speed;
            idleSeconds = speed > TylerMotion.StillSpeed ? 0f : idleSeconds + Time.deltaTime;
            if (player == null)
            {
                ApplyAnimation(speed);
                return;
            }
            Vector3 playerPosition = player.position;
            if (catchUp.Tick(Time.deltaTime, Vector3.Distance(transform.position, playerPosition),
                isActiveAndEnabled, witch != null && witch.chapter == Chapter.Return)) Recover(playerPosition);
            var inputs = TylerInputs.None;
            inputs.HeardMimic = heardMimic;
            inputs.HeardForest = heardForest;
            heardMimic = false;
            heardForest = false;
            inputs.PlayerStill = TylerMotion.IsStill(previousPlayerPosition, playerPosition, Time.deltaTime);
            previousPlayerPosition = playerPosition;
            if (witch != null)
            {
                inputs.WitchForm = witch.Decision.Form;
                inputs.WitchSuspicious = witch.Decision.State == WitchState.Suspicious;
                inputs.WitchDistance = Vector3.Distance(transform.position, witch.transform.position);
            }
            TylerReaction reaction = brain.Tick(Time.deltaTime, in inputs);
            if (reaction != TylerReaction.Growl) forestPoint = null;
            if (reaction != Reaction) EnterReaction(reaction);
            TylerPose pose = TylerPoses.For(Reaction);
            ApplyMovement(pose, playerPosition);
            ApplyPose(pose);
            if (brain.GrowlingAtForest && forestPoint.HasValue) FaceWitch(pose, forestPoint.Value);
            else if (witch != null) FaceWitch(pose, witch.transform.position);
            ApplyAnimation(speed);
        }

        private void SetSharingLight(bool sharing)
        {
            if (SharingLight == sharing) return;
            SharingLight = sharing;
            if (shareLight != null) shareLight.enabled = sharing;
            Debug.Log(sharing ? GameInfo.LogPrefix + " Tyler comparte su luz"
                : GameInfo.LogPrefix + " Tyler deja de compartir su luz");
        }

        private void EnterReaction(TylerReaction reaction)
        {
            Reaction = reaction;
            Debug.Log(reaction switch
            {
                TylerReaction.Guide => GameInfo.LogPrefix + " Tyler: Guide",
                TylerReaction.Whine => GameInfo.LogPrefix + " Tyler: Whine",
                TylerReaction.Growl => GameInfo.LogPrefix + " Tyler: Growl",
                _ => GameInfo.LogPrefix + " Tyler: None"
            });
            AudioClip clip = reaction == TylerReaction.Whine ? whineClip
                : reaction == TylerReaction.Growl ? growlClip : null;
            if (voice != null && clip != null) voice.PlayOneShot(clip);
        }

        private void ApplyMovement(TylerPose pose, Vector3 playerPosition)
        {
            if (!EnableAgent()) return;
            agent.updateRotation = !pose.FaceWitch;
            agent.isStopped = pose.Stopped;
            if (pose.Stopped) return;
            Vector3 target = Reaction == TylerReaction.Guide && nextMemory != null
                ? TylerMotion.GuideTarget(transform.position, playerPosition, nextMemory.position)
                : TylerMotion.FollowTarget(transform.position, playerPosition);
            Vector3 offset = target - transform.position;
            offset.y = 0f;
            agent.speed = TylerMotion.MoveSpeed(Vector3.Distance(transform.position, playerPosition));
            agent.isStopped = offset.sqrMagnitude == 0f;
            if (agent.isStopped) return;
            if (!hasDestination || WitchMotion.ShouldRepath(lastDestination, target, repathThreshold))
            {
                hasDestination = agent.SetDestination(target);
                if (hasDestination) lastDestination = target;
            }
        }

        private bool EnableAgent()
        {
            bool ready = NavMeshPlacement.EnsureOnNavMesh(agent, transform, ref warnedMissingNavMesh,
                GameInfo.LogPrefix + " Tyler no encontró NavMesh cerca.", out bool repositioned);
            if (repositioned) hasDestination = false;
            return ready;
        }

        private void Recover(Vector3 playerPosition)
        {
            Vector3 fallback = default;
            bool found = false;
            for (int i = 0; i < 3; i++)
            {
                Vector3 direction = i == 0 ? -player.forward : i == 1 ? player.right : -player.right;
                Vector3 candidate = playerPosition + direction * (TylerMotion.FollowDistance + 1f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, agent.areaMask)) continue;
                if (!found) fallback = hit.position;
                found = true;
                Vector3 screen = view != null ? view.WorldToViewportPoint(hit.position + Vector3.up * 0.5f) : default;
                if (view != null && screen.z > 0f && screen.x >= 0f && screen.x <= 1f
                    && screen.y >= 0f && screen.y <= 1f) continue;
                fallback = hit.position;
                break;
            }
            if (!found) return;
            agent.enabled = false;
            transform.position = fallback;
            EnableAgent();
            hasDestination = false;
            catchUp.Reset();
            icon.Reset();
            if (voice != null) voice.Stop();
        }

        private void LateUpdate()
        {
            string symbol = icon.Tick(Time.deltaTime, Reaction);
            if (reactionIcon == null) return;
            reactionIcon.enabled = symbol.Length > 0;
            if (!reactionIcon.enabled) return;
            if (reactionIcon.text != symbol)
            {
                reactionIcon.text = symbol;
                reactionIcon.color = TylerIcon.TintFor(symbol);
            }
            Transform canvas = reactionIcon.transform.parent;
            canvas.position = (head != null ? head.position : transform.position) + Vector3.up * 0.6f;
            if (view != null) canvas.rotation = view.transform.rotation;
        }

        public void ShowReaction(TylerReaction reaction, Vector3 witchPosition)
        {
            Reaction = reaction;
            TylerPose pose = TylerPoses.For(reaction);
            ApplyPose(pose);
            FaceWitch(pose, witchPosition);
            ApplyAnimation(Speed);
        }

        private void ApplyAnimation(float speed)
        {
            if (animator == null) return;
            animator.SetFloat(speedParameterHash, speed);
            int stateHash = TylerAnimation.StateFor(Reaction, speed, idleSeconds) switch
            {
                TylerAnimation.Walk => walkHash,
                TylerAnimation.Sit => sitHash,
                TylerAnimation.Whine => whineHash,
                TylerAnimation.Growl => growlHash,
                TylerAnimation.Bark => barkHash,
                _ => idleHash
            };
            if (hasAnimationState && lastAnimationStateHash == stateHash) return;
            animator.CrossFade(stateHash, 0.15f, 0);
            lastAnimationStateHash = stateHash;
            hasAnimationState = true;
        }

        private void ApplyPose(TylerPose pose)
        {
            if (hasPose && lastPose.Crouched == pose.Crouched && lastPose.LookUp == pose.LookUp
                && lastPose.FaceWitch == pose.FaceWitch && lastPose.Stopped == pose.Stopped) return;
            if (figure != null) figure.SetActive(true);
            lastPose = pose;
            hasPose = true;
        }

        private void FaceWitch(TylerPose pose, Vector3 position)
        {
            if (!pose.FaceWitch) return;
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
