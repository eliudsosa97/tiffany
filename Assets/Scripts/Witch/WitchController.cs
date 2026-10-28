using System;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WitchController : MonoBehaviour
    {
        public Transform player;
        public Vector3[] skyRoute;
        public Chapter chapter = Chapter.Ravine;
        public WitchSenseProfile senses = new WitchSenseProfile();
        public GameObject figure;
        public GameObject fireBall;
        public Light fireLight;
        public float fireFullHeight = 14f;
        public LayerMask sightMask = Physics.DefaultRaycastLayers;
        public LayerMask coverMask = Physics.DefaultRaycastLayers;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public float sprintRadius = 0.4f;
        public float landingMargin = 1f;
        public float repathThreshold = 0.5f;
        public bool playerLightOn;
        public Vector3 playerLightPosition;
        public bool playerHidden;
        public bool playerVanished;
        public bool raining;
        public bool hearingMuted;

        public WitchDecision Decision { get; private set; }
        public event Action Caught;

        private NavMeshAgent agent;
        private WitchProfile profile;
        private WitchSenses witchSenses;
        private WitchBrain brain;
        private RaycastHit[] hits;
        private Action<StepNoise> noiseHandler;
        private bool flashed;
        private float stateTime;
        private Vector3 entryPosition;
        private Vector3 verticalTarget;
        private Vector3 lastDestination;
        private bool hasDestination;
        private float baseFireIntensity;
        private bool warnedMissingNavMesh;
        private WitchPose lastPose;
        private bool hasPose;
        private Animator animator;
        private WitchState lastAnimationState;
        private bool hasAnimationState;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int StalkingHash = Animator.StringToHash(nameof(WitchState.Stalking));
        private static readonly int WindUpHash = Animator.StringToHash(nameof(WitchState.WindUp));
        private static readonly int SprintingHash = Animator.StringToHash(nameof(WitchState.Sprinting));
        private static readonly int RecoveringHash = Animator.StringToHash(nameof(WitchState.Recovering));
        private static readonly int StunnedHash = Animator.StringToHash(nameof(WitchState.Stunned));
        private static readonly int CaughtHash = Animator.StringToHash(nameof(WitchState.Caught));
        private static readonly int LandingHash = Animator.StringToHash(nameof(WitchState.Landing));
        private static readonly int TakingOffHash = Animator.StringToHash(nameof(WitchState.TakingOff));

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            witchSenses = new WitchSenses(senses);
            profile = WitchProfiles.For(chapter);
            brain = new WitchBrain(profile, skyRoute);
            hits = new RaycastHit[32];
            noiseHandler = Hear;
            if (figure != null) animator = figure.GetComponent<Animator>();
            if (fireLight != null) baseFireIntensity = fireLight.intensity;
            Decision = new WitchDecision { State = WitchState.Flying, Form = WitchForm.Flying, FireLit = true };
        }

        private void OnEnable() => NoiseBus.Published += noiseHandler;

        private void OnDisable() => NoiseBus.Published -= noiseHandler;

        private void Start()
        {
            if (player != null) return;
            var found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        public void Flash() => flashed = true;

        private void Hear(StepNoise noise) => witchSenses.Hear(in noise, transform.position);

        private void Update()
        {
            bool flashThisFrame = flashed;
            flashed = false;
            if (player == null) return;

            Vector3 position = transform.position;
            Vector3 playerPosition = player.position;
            Vector3 sightOrigin = position + Vector3.up * 1.6f;
            Vector3 sightDirection = playerPosition + Vector3.up - sightOrigin;
            int count = Physics.RaycastNonAlloc(sightOrigin, sightDirection, hits,
                sightDirection.magnitude, sightMask, QueryTriggerInteraction.Ignore);
            bool lineOfSight = !ClosestWorldHit(count, out var sightHit, false)
                || sightHit.collider.transform.IsChildOf(player);
            count = Physics.RaycastNonAlloc(playerPosition + Vector3.up * 2f, Vector3.up, hits,
                40f, coverMask, QueryTriggerInteraction.Ignore);
            bool underCover = ClosestWorldHit(count, out _);
            var inputs = new WitchSenseInputs
            {
                WitchPosition = position,
                Form = Decision.Form,
                FireLit = Decision.FireLit,
                PlayerPosition = playerPosition,
                PlayerUnderCover = underCover,
                LineOfSight = lineOfSight,
                PlayerLightOn = playerLightOn,
                PlayerLightPosition = playerLightPosition,
                PlayerInSafeZone = SafeZones.Contains(playerPosition),
                PlayerHidden = playerHidden,
                PlayerVanished = playerVanished,
                Flashed = flashThisFrame,
                Raining = raining,
                HearingMuted = hearingMuted
            };
            float dt = Time.deltaTime;
            WitchState previousState = Decision.State;
            Decision = brain.Tick(dt, witchSenses.Sense(in inputs));
            stateTime += dt;
            if (Decision.State != previousState)
            {
                if (previousState == WitchState.Landing) transform.position = verticalTarget;
                EnterState();
            }
            ApplyMovement(dt);
            ApplyView(position, dt);
        }

        private void EnterState()
        {
            entryPosition = transform.position;
            stateTime = 0f;
            if (Decision.State == WitchState.Landing)
            {
                verticalTarget = GroundPosition(SafeZones.PushOut(entryPosition, landingMargin));
                if (TrySampleNavMesh(verticalTarget, 6f, out var hit))
                    verticalTarget = hit.position;
            }
            else if (Decision.State == WitchState.TakingOff)
            {
                verticalTarget = entryPosition;
                verticalTarget.y = WitchMotion.CruiseHeight(skyRoute, entryPosition, entryPosition.y + 12f);
            }
            else if (Decision.State == WitchState.Caught)
            {
                Caught?.Invoke();
            }
            Debug.Log($"{GameInfo.LogPrefix} Bruja: {Decision.State}");
        }

        private void ApplyMovement(float dt)
        {
            switch (Decision.State)
            {
                case WitchState.Flying:
                    agent.enabled = false;
                    transform.position = WitchMotion.Fly(transform.position, Decision.MoveTarget, Decision.Speed, dt);
                    break;
                case WitchState.Landing:
                    agent.enabled = false;
                    transform.position = WitchMotion.Vertical(entryPosition, verticalTarget, stateTime, profile.LandingDuration);
                    break;
                case WitchState.TakingOff:
                    agent.enabled = false;
                    transform.position = WitchMotion.Vertical(entryPosition, verticalTarget, stateTime, profile.TakeOffDuration);
                    break;
                case WitchState.Stalking:
                    Stalk();
                    break;
                case WitchState.WindUp:
                case WitchState.Recovering:
                case WitchState.Stunned:
                case WitchState.Caught:
                    if (EnableAgent()) agent.isStopped = true;
                    if (Decision.State == WitchState.WindUp)
                    {
                        Vector3 direction = Decision.MoveTarget - transform.position;
                        direction.y = 0f;
                        if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
                    }
                    break;
                case WitchState.Sprinting:
                    agent.enabled = false;
                    Sprint(dt);
                    break;
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
                    Debug.LogWarning($"{GameInfo.LogPrefix} La bruja no encontró NavMesh cerca de {transform.position}.");
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
            {
                if (NavMesh.SamplePosition(position, out hit, radius, agent.areaMask)) return true;
            }
            hit = default;
            return false;
        }

        private void Stalk()
        {
            if (!EnableAgent()) return;
            agent.isStopped = Decision.Speed <= 0f;
            if (agent.isStopped) return;
            agent.speed = Decision.Speed;
            Vector3 destination = SafeZones.PushOut(Decision.MoveTarget, landingMargin);
            if (!hasDestination || WitchMotion.ShouldRepath(lastDestination, destination, repathThreshold))
            {
                agent.SetDestination(destination);
                lastDestination = destination;
                hasDestination = true;
            }
        }

        private void Sprint(float dt)
        {
            Vector3 position = transform.position;
            Vector3 direction = Decision.MoveTarget - position;
            direction.y = 0f;
            float freeDistance = float.PositiveInfinity;
            if (direction.sqrMagnitude > 0f && Decision.Speed * dt > 0f)
            {
                int count = Physics.SphereCastNonAlloc(position + Vector3.up, sprintRadius, direction.normalized,
                    hits, Decision.Speed * dt, sightMask, QueryTriggerInteraction.Ignore);
                if (ClosestWorldHit(count, out var hit)) freeDistance = hit.distance;
            }
            Vector3 next = WitchMotion.SprintStep(position, Decision.MoveTarget, Decision.Speed, dt, freeDistance);
            if (NavMesh.SamplePosition(position, out _, 1f, agent.areaMask)
                && NavMesh.Raycast(position, next, out var edge, agent.areaMask)) next = edge.position;
            if (!SafeZones.Contains(next)) transform.position = GroundPosition(next);
        }

        private Vector3 GroundPosition(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 2f, Vector3.down, hits,
                Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
            if (ClosestWorldHit(count, out var hit)) position.y = hit.point.y;
            return position;
        }

        private bool ClosestWorldHit(int count, out RaycastHit closest, bool ignorePlayer = true)
        {
            closest = default;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if ((ignorePlayer && hit.collider.transform.IsChildOf(player)) || hit.collider.transform.IsChildOf(transform)
                    || hit.distance >= distance) continue;
                closest = hit;
                distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }

        private void ApplyView(Vector3 previousPosition, float dt)
        {
            ApplyPose(WitchPoses.For(Decision.State, Decision.FireLit));
            Vector3 velocity = agent.enabled && agent.isOnNavMesh ? agent.velocity
                : Decision.State == WitchState.Sprinting && dt > 0f ? (transform.position - previousPosition) / dt : Vector3.zero;
            velocity.y = 0f;
            ApplyAnimation(Decision.State, velocity.magnitude);
            if (fireLight != null && fireLight.enabled)
                fireLight.intensity = WitchPoses.FireIntensity(baseFireIntensity, FireHeightAboveGround(), fireFullHeight);
        }

        public void ShowPose(WitchState state, bool fireLit)
        {
            ApplyPose(WitchPoses.For(state, fireLit));
            float speed = state == WitchState.Sprinting ? profile.SprintSpeed
                : state == WitchState.Stalking ? profile.StalkSpeed : 0f;
            ApplyAnimation(state, speed, true);
            if (fireLight == null) return;
            fireLight.intensity = WitchPoses.FireIntensity(baseFireIntensity, FireHeightAboveGround(), fireFullHeight);
        }

        private float FireHeightAboveGround()
        {
            int count = Physics.RaycastNonAlloc(fireLight.transform.position, Vector3.down, hits,
                Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
            return ClosestWorldHit(count, out var hit) ? hit.distance : fireFullHeight;
        }

        private void ApplyPose(WitchPose pose)
        {
            if (hasPose && lastPose.FigureVisible == pose.FigureVisible && lastPose.BallVisible == pose.BallVisible
                && lastPose.LightOn == pose.LightOn) return;

            if (fireBall != null) fireBall.SetActive(pose.BallVisible);
            if (fireLight != null) fireLight.enabled = pose.LightOn;
            if (figure != null) figure.SetActive(pose.FigureVisible);
            lastPose = pose;
            hasPose = true;
        }

        private void ApplyAnimation(WitchState state, float speed, bool immediate = false)
        {
            if (animator == null) return;
            if (!animator.isActiveAndEnabled)
            {
                hasAnimationState = false;
                return;
            }
            animator.SetFloat(SpeedHash, speed);
            if (!immediate && hasAnimationState && lastAnimationState == state) return;
            int hash = state switch
            {
                WitchState.Stalking => StalkingHash,
                WitchState.WindUp => WindUpHash,
                WitchState.Sprinting => SprintingHash,
                WitchState.Recovering => RecoveringHash,
                WitchState.Stunned => StunnedHash,
                WitchState.Caught => CaughtHash,
                WitchState.Landing => LandingHash,
                WitchState.TakingOff => TakingOffHash,
                _ => IdleHash
            };
            if (immediate) animator.Play(hash, 0, 0f);
            else
            {
                float duration = state == WitchState.Sprinting ? 0.1f : 0.25f;
                animator.CrossFadeInFixedTime(hash, duration, 0, 0f);
            }
            lastAnimationState = state;
            hasAnimationState = true;
        }
    }
}
