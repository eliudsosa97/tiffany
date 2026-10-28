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

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            witchSenses = new WitchSenses(senses);
            profile = WitchProfiles.For(chapter);
            brain = new WitchBrain(profile, skyRoute);
            hits = new RaycastHit[32];
            noiseHandler = Hear;
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

        private void Hear(StepNoise noise) => witchSenses.Hear(in noise);

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
            ApplyView();
        }

        private void EnterState()
        {
            entryPosition = transform.position;
            stateTime = 0f;
            if (Decision.State == WitchState.Landing)
            {
                verticalTarget = GroundPosition(SafeZones.PushOut(entryPosition, landingMargin));
                if (TrySampleNavMesh(verticalTarget, 6f, out var hit) && !SafeZones.Contains(hit.position))
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
            if (!TrySampleNavMesh(transform.position, 3f, out var hit)) return false;
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

        private void ApplyView()
        {
            if (fireBall != null) fireBall.SetActive(Decision.FireLit);
            if (fireLight != null) fireLight.enabled = Decision.FireLit;
            if (figure == null) return;
            figure.SetActive(Decision.Form == WitchForm.Grounded);
            figure.transform.localScale = Decision.State == WitchState.WindUp
                ? new Vector3(1.15f, 0.75f, 1.15f) : Vector3.one;
        }
    }
}
