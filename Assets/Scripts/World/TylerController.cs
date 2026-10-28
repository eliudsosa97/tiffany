using UnityEngine;
using UnityEngine.AI;

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
        public GameObject standBody;
        public GameObject crouchBody;
        public AudioSource voice;
        public AudioClip whineClip;
        public AudioClip growlClip;
        public Animator animator;
        public float repathThreshold = 0.5f;
        public float nearPlayerRadius = 6f;

        public TylerReaction Reaction { get; private set; }
        public bool NearPlayer
        {
            get
            {
                if (player == null) return false;
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
        private bool heardMimic;
        private NavMeshAgent agent;
        private Vector3 previousPlayerPosition;
        private Vector3 lastDestination;
        private Quaternion initialHeadRotation;
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
            initialHeadRotation = head != null ? head.localRotation : Quaternion.identity;
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
            if (player != null) previousPlayerPosition = player.position;
        }

        private void OnDisable()
        {
            if (agent != null) agent.enabled = false;
        }

        private void Start()
        {
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }
            if (player != null) previousPlayerPosition = player.position;
        }

        public void HearMimic() => heardMimic = true;

        private void Update()
        {
            float speed = Speed;
            idleSeconds = speed > TylerMotion.StillSpeed ? 0f : idleSeconds + Time.deltaTime;
            if (player == null)
            {
                ApplyAnimation(speed);
                return;
            }
            Vector3 playerPosition = player.position;
            var inputs = TylerInputs.None;
            inputs.HeardMimic = heardMimic;
            heardMimic = false;
            inputs.PlayerStill = TylerMotion.IsStill(previousPlayerPosition, playerPosition, Time.deltaTime);
            previousPlayerPosition = playerPosition;
            if (witch != null)
            {
                inputs.WitchForm = witch.Decision.Form;
                inputs.WitchDistance = Vector3.Distance(transform.position, witch.transform.position);
            }
            TylerReaction reaction = brain.Tick(Time.deltaTime, in inputs);
            if (reaction != Reaction) EnterReaction(reaction);
            TylerPose pose = TylerPoses.For(Reaction);
            ApplyMovement(pose, playerPosition);
            ApplyPose(pose);
            if (witch != null) FaceWitch(pose, witch.transform.position);
            ApplyAnimation(speed);
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
            agent.speed = TylerMotion.MoveSpeed(offset.magnitude);
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
            if (agent.enabled)
            {
                if (agent.isOnNavMesh) return true;
                agent.enabled = false;
            }
            if (!TrySampleNavMesh(transform.position, 3f, out var hit))
            {
                if (!warnedMissingNavMesh)
                {
                    Debug.LogWarning(GameInfo.LogPrefix + " Tyler no encontró NavMesh cerca.");
                    warnedMissingNavMesh = true;
                }
                return false;
            }
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            hasDestination = false;
            if (agent.isOnNavMesh) return true;
            agent.enabled = false;
            return false;
        }

        private bool TrySampleNavMesh(Vector3 position, float radius, out NavMeshHit hit)
        {
            for (; radius <= 24f; radius *= 2f)
                if (NavMesh.SamplePosition(position, out hit, radius, agent.areaMask)) return true;
            hit = default;
            return false;
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
            if (standBody != null) standBody.SetActive(!pose.Crouched);
            if (crouchBody != null) crouchBody.SetActive(pose.Crouched);
            if (head != null && !pose.LookUp) head.localRotation = initialHeadRotation;
            lastPose = pose;
            hasPose = true;
        }

        private void FaceWitch(TylerPose pose, Vector3 position)
        {
            if (!pose.FaceWitch) return;
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
            if (pose.LookUp && head != null)
            {
                direction = position - head.position;
                if (direction.sqrMagnitude > 0f) head.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}
