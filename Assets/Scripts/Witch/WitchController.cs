using System;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class WitchController : MonoBehaviour
    {
        public Transform player;
        public Vector3[] skyRoute;
        public Chapter chapter = Chapter.Stable;
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
        public float EffectiveSpeed => Decision.Speed * jerk.SpeedFactor;
        public WitchPerception Perception { get; private set; }
        public Vector3? NextClearingOverride { get; set; }
        public WitchProfile Profile => profile;
        public bool Chasing => brain != null && brain.Chasing;
        public Transform WatchPassage { get; private set; }
        public bool WatchInvestigating => brain.Watch != null && brain.Watch.Investigating;
        public bool WatchPassageClear => brain.Watch != null && brain.Watch.PassageClear(transform.position);
        public event Action Caught;

        private NavMeshAgent agent;
        private CapsuleCollider watchBody;
        private WitchProfile profile;
        private WitchSenses witchSenses;
        private WitchBrain brain;
        private WitchChapterTracker chapterTracker;
        private RespawnPoint respawn;
        private Vector3[] hidePoints;
        private RaycastHit[] hits;
        private Action<StepNoise> noiseHandler;
        private bool flashed;
        private float stateTime;
        private Vector3 entryPosition;
        private Vector3 verticalTarget;
        private bool landingOnTarget;
        private Vector3 lastDestination;
        private bool hasDestination;
        private float baseFireIntensity;
        private bool warnedMissingNavMesh;
        private WitchPose lastPose;
        private bool hasPose;
        private Animator animator;
        private WitchState lastAnimationState;
        private bool hasAnimationState;
        private readonly WitchJerk jerk = new WitchJerk(Environment.TickCount);
        private Vector3 figurePosition;
        private Quaternion figureRotation = Quaternion.identity;
        private bool trembling;
        private const float TrembleOffset = 0.05f, TrembleAngle = 5f;
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
            watchBody = GetComponent<CapsuleCollider>();
            watchBody.height = 2.3f;
            watchBody.radius = 0.5f;
            watchBody.center = Vector3.up * 1.15f;
            watchBody.enabled = false;
            hits = new RaycastHit[32];
            noiseHandler = Hear;
            if (figure != null)
            {
                animator = figure.GetComponent<Animator>();
                figure.transform.GetLocalPositionAndRotation(out figurePosition, out figureRotation);
            }
            if (fireLight != null) baseFireIntensity = fireLight.intensity;
            InitializeBrain();
        }

        private void OnEnable() => NoiseBus.Published += noiseHandler;

        private void OnDisable()
        {
            NoiseBus.Published -= noiseHandler;
            if (animator != null) animator.speed = 1f;
            if (figure != null) figure.transform.SetLocalPositionAndRotation(figurePosition, figureRotation);
            trembling = false;
        }

        private void Start()
        {
            var volumes = FindObjectsByType<HideVolume>(FindObjectsSortMode.None);
            hidePoints = new Vector3[volumes.Length];
            for (int i = 0; i < volumes.Length; i++) hidePoints[i] = volumes[i].HidePoint;
            witchSenses = new WitchSenses(senses, hidePoints);
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null) player = found.transform;
            }
            chapterTracker = FindAnyObjectByType<WitchChapterTracker>();
            if (player != null && player.TryGetComponent<PlayerSoul>(out var soul)) respawn = soul.Respawn;
        }

        public void Flash() => flashed = true;

        public void ResetBrain()
        {
            WatchPassage = null;
            InitializeBrain();
            if (profile.GroundChase)
            {
                PlaceForGroundChase();
            }
            else
            {
                Vector3 fallback = transform.position;
                fallback.y = WitchMotion.CruiseHeight(skyRoute, fallback, fallback.y + 12f);
                transform.position = WitchMotion.FarthestRoutePoint(skyRoute,
                    player != null ? player.position : transform.position, fallback) + Vector3.up * profile.FlightHeightOffset;
            }
            ApplyView(transform.position, 0f);
            Debug.Log($"{GameInfo.LogPrefix} Bruja: {(profile.GroundChase ? "retoma la persecución a pie" : "vuelve a la ruta")}");
        }

        public void BeginReturn()
        {
            chapter = Chapter.Return;
            ResetBrain();
        }

        public void BeginWatch(Transform passage, Vector3 post, bool requiresVanish, bool blocksPassage = false)
        {
            InitializeBrain();
            WatchPassage = passage;
            watchBody.enabled = blocksPassage;
            transform.SetPositionAndRotation(post, Quaternion.LookRotation(-passage.forward));
            Physics.SyncTransforms();
            brain.BeginWatch(post, requiresVanish);
            Decision = new WitchDecision { State = WitchState.Stalking, Form = WitchForm.Grounded, MoveTarget = post };
            ApplyView(transform.position, 0f);
        }

        public void EndWatch() => ResetBrain();

        public void SetChapter(Chapter next)
        {
            if (next <= chapter) return;
            if (next == Chapter.Return)
            {
                BeginReturn();
                return;
            }
            SetProfile(WitchProfiles.For(next));
            chapter = next;
        }

        public void SetProfile(WitchProfile next)
        {
            profile = next ?? throw new ArgumentNullException(nameof(next));
            brain.SetProfile(profile);
        }

        private void InitializeBrain()
        {
            profile = WitchProfiles.For(chapter);
            brain = new WitchBrain(profile, skyRoute, resolveGroundTarget: ResolveGroundTarget);
            witchSenses = new WitchSenses(senses, hidePoints);
            agent.enabled = false;
            watchBody.enabled = false;
            hasDestination = hasPose = flashed = false;
            stateTime = 0f;
            Decision = new WitchDecision
            {
                State = profile.GroundChase ? WitchState.Stalking : WitchState.Flying,
                Form = profile.GroundChase ? WitchForm.Grounded : WitchForm.Flying,
                FireLit = !profile.GroundChase
            };
        }

        private void PlaceForGroundChase()
        {
            if (player == null || TryPlaceForGroundChase(profile.ReturnPlacementDistance)) return;
            TryPlaceForGroundChase(12f);
        }

        private bool TryPlaceForGroundChase(float distance)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.AngleAxis(i * 45f, Vector3.up) * -player.forward * distance;
                if (!SafeZones.TryPushOut(player.position + offset, landingMargin, out var entry)
                    || !NavMeshPlacement.TrySample(entry, 6f, agent.areaMask, out var hit)
                    || SafeZones.Contains(hit.position)) continue;
                Vector3 separation = hit.position - player.position;
                separation.y = 0f;
                if (separation.sqrMagnitude < profile.SprintTriggerDistance * profile.SprintTriggerDistance) continue;
                transform.position = hit.position;
                return true;
            }
            return false;
        }

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
                HearingMuted = hearingMuted,
                PlayerForward = player.forward,
                HasNextClearing = NextClearingOverride.HasValue || (chapterTracker != null && chapterTracker.HasNextClearing),
                NextClearingPosition = NextClearingOverride ?? (chapterTracker != null ? chapterTracker.NextClearingPosition : Vector3.zero),
                HasFamilyCandle = respawn != null && respawn.HasFamilyCandle,
                FamilyCandlePosition = respawn != null ? respawn.Position : Vector3.zero,
                FamilyCandleRadius = respawn != null ? respawn.FamilyCandleRadius : 0f
            };
            float dt = Time.deltaTime;
            WitchState previousState = Decision.State;
            WitchIntent previousIntent = Decision.Intent;
            bool wasInspectingHide = Decision.InspectingHide;
            var perception = witchSenses.Sense(in inputs);
            if (WatchPassage != null)
            {
                Vector3 local = WatchPassage.InverseTransformPoint(playerPosition);
                perception.InWatchPassage = local.z >= 0f && local.z <= ChallengeRules.PassageLength
                    && Mathf.Abs(local.x) <= 2.5f;
            }
            Perception = perception;
            Decision = brain.Tick(dt, in perception);
            stateTime += dt;
            if (Decision.State != previousState)
            {
                if (previousState == WitchState.Landing && Decision.State == WitchState.Stalking)
                    transform.position = verticalTarget;
                EnterState();
            }
            if (Decision.InspectingHide && !wasInspectingHide)
                Debug.Log($"{GameInfo.LogPrefix} Bruja: revisa escondite en {Decision.MoveTarget}");
            jerk.Tick(dt, Decision);
            if (Decision.Intent != previousIntent)
                Debug.Log($"{GameInfo.LogPrefix} Bruja: {IntentDescription(Decision.Intent)}");
            ApplyMovement(dt);
            ApplyView(position, dt);
        }

        private void EnterState()
        {
            entryPosition = transform.position;
            stateTime = 0f;
            landingOnTarget = Decision.State == WitchState.Landing
                && (Decision.Intent == WitchIntent.Ambush || Decision.Intent == WitchIntent.LastSeen);
            if (Decision.State == WitchState.Landing)
            {
                if (landingOnTarget)
                    verticalTarget = Decision.MoveTarget;
                else
                {
                    verticalTarget = GroundPosition(SafeZones.PushOut(entryPosition, landingMargin));
                    if (NavMeshPlacement.TrySample(verticalTarget, 6f, agent.areaMask, out var hit)
                        && !SafeZones.Contains(hit.position)) verticalTarget = hit.position;
                }
            }
            else if (Decision.State == WitchState.TakingOff)
            {
                verticalTarget = entryPosition;
                verticalTarget.y = WitchMotion.CruiseHeight(skyRoute, entryPosition, entryPosition.y + 12f)
                    + profile.FlightHeightOffset;
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
                    if (landingOnTarget && Decision.MoveTarget != verticalTarget)
                    {
                        entryPosition = transform.position;
                        verticalTarget = Decision.MoveTarget;
                        stateTime = 0f;
                    }
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
                case WitchState.Suspicious:
                    agent.enabled = false;
                    hasDestination = false;
                    Vector3 direction = Decision.MoveTarget - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
                    break;
                case WitchState.Recovering:
                case WitchState.Stunned:
                case WitchState.Caught:
                    if (EnableAgent()) agent.isStopped = true;
                    break;
                case WitchState.Sprinting:
                    agent.enabled = false;
                    Sprint(dt);
                    break;
            }
        }

        private bool EnableAgent()
        {
            bool ready = NavMeshPlacement.EnsureOnNavMesh(agent, transform, ref warnedMissingNavMesh,
                GameInfo.LogPrefix + " La bruja no encontró NavMesh cerca de {0}.", out bool repositioned);
            if (repositioned) hasDestination = false;
            return ready;
        }

        private Vector3 ResolveGroundTarget(Vector3 position)
        {
            if (SafeZones.TryPushOut(position, landingMargin, out var destination)
                && NavMeshPlacement.TrySample(destination, 6f, agent.areaMask, out var hit)
                && !SafeZones.Contains(hit.position)) return hit.position;
            return Vector3.positiveInfinity;
        }

        private static string IntentDescription(WitchIntent intent)
        {
            return intent switch
            {
                WitchIntent.Search => "busca en espiral",
                WitchIntent.Ambush => "se adelanta al siguiente claro",
                WitchIntent.CandleWatch => "ronda la veladora",
                WitchIntent.BarkLure => "prepara el ladrido desde el bosque",
                WitchIntent.LastSeen => "va al último punto visto",
                _ => "retoma su conducta habitual"
            };
        }

        private void Stalk()
        {
            if (!EnableAgent()) return;
            float speed = EffectiveSpeed;
            if (speed <= 0f && !agent.isStopped) agent.velocity = Vector3.zero;
            agent.isStopped = speed <= 0f;
            if (Decision.Speed <= 0f) return;
            if (!agent.isStopped) agent.speed = speed;
            Vector3 destination = Decision.MoveTarget;
            if (!hasDestination || WitchMotion.ShouldRepath(lastDestination, destination, repathThreshold))
            {
                agent.SetDestination(destination);
                lastDestination = destination;
                hasDestination = true;
            }
            if (agent.isStopped || !jerk.PhaseStarted) return;
            Vector3 direction = (agent.hasPath ? agent.steeringTarget : destination) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) agent.velocity = direction.normalized * speed;
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
            WitchState poseState = Decision.State == WitchState.Suspicious && Decision.Form == WitchForm.Flying
                ? WitchState.Flying : Decision.State;
            ApplyPose(WitchPoses.For(poseState, Decision.FireLit));
            Vector3 velocity = agent.enabled && agent.isOnNavMesh ? agent.velocity
                : Decision.State == WitchState.Sprinting && dt > 0f ? (transform.position - previousPosition) / dt : Vector3.zero;
            velocity.y = 0f;
            bool frozen = Decision.State == WitchState.Stalking
                && (jerk.Phase == WitchJerkPhase.Still || jerk.Phase == WitchJerkPhase.Tremble);
            ApplyAnimation(Decision.State, frozen ? Decision.Speed : velocity.magnitude);
            ApplyTremble();
            if (fireLight != null && fireLight.enabled)
                fireLight.intensity = WitchPoses.FireIntensity(baseFireIntensity, FireHeightAboveGround(), fireFullHeight);
        }

        private void ApplyTremble()
        {
            float amount = jerk.Tremble;
            if (figure == null || (amount <= 0f && !trembling)) return;
            trembling = amount > 0f;
            float t = Time.time;
            Vector3 offset = new Vector3(Mathf.Sin(t * 83f), 0f, Mathf.Sin(t * 71f + 1.7f)) * (amount * TrembleOffset);
            Quaternion shake = Quaternion.Euler(Mathf.Sin(t * 97f + 0.5f) * amount * TrembleAngle,
                Mathf.Sin(t * 61f) * amount * TrembleAngle * 1.5f, Mathf.Sin(t * 89f + 2.3f) * amount * TrembleAngle);
            figure.transform.SetLocalPositionAndRotation(figurePosition + offset, figureRotation * shake);
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
            animator.speed = immediate ? 1f : jerk.AnimatorSpeed;
            animator.SetFloat(SpeedHash, speed);
            if (!immediate && hasAnimationState && lastAnimationState == state) return;
            int hash = state switch
            {
                WitchState.Stalking => StalkingHash,
                // Provisional hasta que E haga el clip de sospecha: Stalking con Speed 0 usa Idle.
                WitchState.Suspicious => StalkingHash,
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
