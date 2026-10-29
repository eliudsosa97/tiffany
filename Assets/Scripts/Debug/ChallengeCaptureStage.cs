using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class ChallengeCaptureStage : MonoBehaviour
    {
        public PowerChallenge challenge;
        public bool after;
        PowerChallenge[] challenges;
        bool[] challengeEnabled, gateActive, candleEnabled, flameEnabled, meshEnabled;
        CandleSpot[] candles;
        WitchController witch;
        NavMeshAgent agent;
        bool applied, witchEnabled, witchActive, agentEnabled, agentStopped, agentHadPath, trackerEnabled;
        Vector3 witchPosition, destination, objectPosition, velocity, angularVelocity;
        Quaternion witchRotation, objectRotation;
        bool objectKinematic;
        Rigidbody body;
        float timeScale;
        VanishPower vanish;
        bool[] figureEnabled;
        Light playerSpot;
        GameObject glow;
        bool spotEnabled, glowActive;

#if UNITY_EDITOR
        struct CameraTarget
        {
            public Transform owner;
            public Bounds bounds;

            public CameraTarget(Transform owner, Vector3 position, Vector3 size)
            {
                this.owner = owner;
                bounds = new Bounds(position, size);
            }
        }

        [ContextMenu("Frame Challenge Camera")]
        public void FrameCamera()
        {
            Physics.SyncTransforms();
            var root = challenge.transform;
            var targets = new System.Collections.Generic.List<CameraTarget>
            {
                new CameraTarget(challenge.tracker.player, challenge.Entry + Vector3.up * 0.6f,
                    new Vector3(0.7f, 1.2f, 0.7f)),
                new CameraTarget(null, challenge.Exit + Vector3.up * 0.1f, new Vector3(0.7f, 0.2f, 0.7f)),
                CandleTarget(challenge.checkpoint)
            };
            foreach (var candle in challenge.candles) targets.Add(CandleTarget(candle));
            foreach (var gate in challenge.darkness)
            {
                var bounds = gate.GetComponent<Renderer>().bounds;
                targets.Add(new CameraTarget(gate.transform, bounds.center, bounds.size));
            }
            if (challenge.kind != ChallengeKind.Candles)
            {
                var witch = challenge.Witch != null ? challenge.Witch.transform : null;
                targets.Add(new CameraTarget(witch, challenge.post.position + Vector3.up * 1.15f,
                    new Vector3(1f, 2.3f, 1f)));
                if (challenge.lure != null)
                {
                    Vector3 position = challenge.lure.transform.position;
                    position.y = challenge.lure.GetComponent<Collider>().bounds.min.y;
                    targets.Add(new CameraTarget(witch, position + Vector3.up * 1.15f, new Vector3(1f, 2.3f, 1f)));
                }
            }
            if (challenge.lure != null)
            {
                var bounds = challenge.lure.GetComponent<Renderer>().bounds;
                targets.Add(new CameraTarget(challenge.lure.transform, bounds.center, bounds.size));
                targets.Add(new CameraTarget(challenge.lure.transform, bounds.center + root.right * 0.35f, bounds.size));
            }

            var frame = targets[0].bounds;
            foreach (var target in targets) frame.Encapsulate(target.bounds);
            foreach (var renderer in challenge.GetComponentsInChildren<Renderer>(true)) frame.Encapsulate(renderer.bounds);
            string obstruction = null;
            for (int elevation = 0; elevation < 4; elevation++)
            {
                float pitch = (55f + elevation * 9f) * Mathf.Deg2Rad;
                for (int side = 0; side < 4; side++)
                {
                    Vector3 diagonal = (root.right * (side % 2 == 0 ? -1f : 1f)
                        + root.forward * (side < 2 ? -1f : 1f)).normalized;
                    var rotation = Quaternion.LookRotation(-diagonal * Mathf.Cos(pitch) - Vector3.up * Mathf.Sin(pitch));
                    Vector3 position = FitCamera(frame, rotation);
                    if (!HasClearView(position, targets, out obstruction)) continue;
                    foreach (var stage in challenge.GetComponentsInChildren<ChallengeCaptureStage>())
                    {
                        var stop = stage.GetComponent<CaptureStop>();
                        stop.overrideCamera = true;
                        stop.cameraPosition = position;
                        stop.cameraRotation = rotation;
                    }
                    return;
                }
            }
            throw new System.InvalidOperationException($"{GameInfo.LogPrefix} {challenge.name}: no hay encuadre sin oclusiones. {obstruction}");
        }

        static CameraTarget CandleTarget(CandleSpot candle) =>
            new CameraTarget(candle.transform, candle.transform.position + Vector3.up * 0.15f,
                new Vector3(0.25f, 0.3f, 0.25f));

        static Vector3 FitCamera(Bounds frame, Quaternion rotation)
        {
            float vertical = Mathf.Tan(24f * Mathf.Deg2Rad * 0.5f) * 0.85f;
            float horizontal = vertical * (16f / 9f);
            var inverse = Quaternion.Inverse(rotation);
            float distance = 1f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(frame.extents, new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 local = inverse * offset;
                distance = Mathf.Max(distance, Mathf.Abs(local.x) / horizontal - local.z,
                    Mathf.Abs(local.y) / vertical - local.z);
            }
            return frame.center - rotation * Vector3.forward * distance;
        }

        static bool HasClearView(Vector3 position, System.Collections.Generic.List<CameraTarget> targets,
            out string obstruction)
        {
            foreach (var target in targets)
                for (int sample = 0; sample < 3; sample++)
                {
                    Vector3 point = target.bounds.center + Vector3.up * (target.bounds.size.y * (sample - 1) * 0.3f);
                    Vector3 ray = point - position;
                    var hits = Physics.RaycastAll(position, ray.normalized, ray.magnitude - 0.02f,
                        Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    foreach (var hit in hits)
                    {
                        if (target.owner != null && hit.transform.IsChildOf(target.owner)) continue;
                        var renderer = hit.collider.GetComponent<Renderer>();
                        if (renderer == null || !renderer.enabled) continue;
                        obstruction = $"{hit.collider.name} tapa {(target.owner != null ? target.owner.name : "el paso o la bruja")}.";
                        return false;
                    }
                }
            obstruction = null;
            return true;
        }
#endif

        public void Apply()
        {
            Clear();
            applied = true;
            timeScale = Time.timeScale;
            Time.timeScale = 0f;
            vanish = challenge.tracker.player.GetComponent<VanishPower>();
            if (vanish != null)
            {
                figureEnabled = new bool[vanish.figure.Length];
                for (int i = 0; i < figureEnabled.Length; i++) figureEnabled[i] = vanish.figure[i].enabled;
                if (vanish.playerLight != null)
                {
                    playerSpot = vanish.playerLight.spot;
                    glow = vanish.playerLight.glow.gameObject;
                    spotEnabled = playerSpot.enabled;
                    glowActive = glow.activeSelf;
                }
            }
            var tracker = challenge.tracker;
            trackerEnabled = tracker.enabled;
            tracker.enabled = false;
            challenges = tracker.GetComponentsInChildren<PowerChallenge>();
            challengeEnabled = new bool[challenges.Length];
            for (int i = 0; i < challenges.Length; i++)
            {
                challengeEnabled[i] = challenges[i].enabled;
                challenges[i].enabled = false;
            }
            candles = tracker.GetComponentsInChildren<CandleSpot>();
            candleEnabled = new bool[candles.Length];
            flameEnabled = new bool[candles.Length];
            meshEnabled = new bool[candles.Length];
            for (int i = 0; i < candles.Length; i++)
            {
                candleEnabled[i] = candles[i].enabled;
                flameEnabled[i] = candles[i].flame != null && candles[i].flame.enabled;
                meshEnabled[i] = candles[i].flameMesh != null && candles[i].flameMesh.enabled;
                candles[i].enabled = false;
            }
            for (int i = 0; i < challenge.candles.Length; i++)
            {
                var candle = challenge.candles[i];
                if (candle.flame != null) candle.flame.enabled = after;
                if (candle.flameMesh != null) candle.flameMesh.enabled = after;
            }
            gateActive = new bool[challenge.darkness.Length];
            for (int i = 0; i < gateActive.Length; i++)
            {
                gateActive[i] = challenge.darkness[i].activeSelf;
                challenge.darkness[i].SetActive(!after);
            }
            witch = challenge.Witch;
            if (witch != null)
            {
                witchEnabled = witch.enabled;
                witchActive = witch.gameObject.activeSelf;
                witchPosition = witch.transform.position;
                witchRotation = witch.transform.rotation;
                agent = witch.GetComponent<NavMeshAgent>();
                agentEnabled = agent.enabled;
                agentHadPath = agent.enabled && agent.isOnNavMesh && agent.hasPath;
                agentStopped = agent.enabled && agent.isOnNavMesh && agent.isStopped;
                if (agentHadPath) destination = agent.destination;
                witch.enabled = agent.enabled = false;
                witch.gameObject.SetActive(true);
                if (challenge.kind != ChallengeKind.Candles)
                {
                    Vector3 position = after && challenge.lure != null ? challenge.lure.transform.position : challenge.post.position;
                    if (after && challenge.lure != null) position.y = challenge.lure.GetComponent<Collider>().bounds.min.y;
                    witch.transform.SetPositionAndRotation(position, challenge.transform.rotation * Quaternion.Euler(0f, after ? 90f : 180f, 0f));
                    witch.ShowPose(WitchState.Stalking, false);
                }
            }
            if (challenge.lure != null)
            {
                body = challenge.lure.GetComponent<Rigidbody>();
                objectPosition = body.position;
                objectRotation = body.rotation;
                velocity = body.linearVelocity;
                angularVelocity = body.angularVelocity;
                objectKinematic = body.isKinematic;
                body.isKinematic = true;
                if (after) challenge.lure.transform.SetPositionAndRotation(objectPosition + challenge.transform.right * 0.35f,
                    objectRotation * Quaternion.Euler(0f, 0f, 80f));
            }
            Physics.SyncTransforms();
        }

        public void Clear()
        {
            if (!applied) return;
            applied = false;
            if (vanish != null)
                for (int i = 0; i < figureEnabled.Length; i++) vanish.figure[i].enabled = figureEnabled[i];
            if (playerSpot != null) playerSpot.enabled = spotEnabled;
            if (glow != null) glow.SetActive(glowActive);
            for (int i = 0; i < gateActive.Length; i++) challenge.darkness[i].SetActive(gateActive[i]);
            if (body != null)
            {
                body.position = objectPosition;
                body.rotation = objectRotation;
                body.transform.SetPositionAndRotation(objectPosition, objectRotation);
                body.isKinematic = objectKinematic;
                if (!objectKinematic)
                {
                    body.linearVelocity = velocity;
                    body.angularVelocity = angularVelocity;
                }
            }
            if (witch != null)
            {
                witch.transform.SetPositionAndRotation(witchPosition, witchRotation);
                witch.ShowPose(witch.Decision.State, witch.Decision.FireLit);
                agent.enabled = agentEnabled;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    if (agentHadPath) agent.SetDestination(destination);
                    agent.isStopped = agentStopped;
                }
                witch.enabled = witchEnabled;
                witch.gameObject.SetActive(witchActive);
            }
            for (int i = 0; i < candles.Length; i++)
            {
                candles[i].enabled = candleEnabled[i];
                if (candles[i].flame != null) candles[i].flame.enabled = flameEnabled[i];
                if (candles[i].flameMesh != null) candles[i].flameMesh.enabled = meshEnabled[i];
            }
            for (int i = 0; i < challenges.Length; i++) challenges[i].enabled = challengeEnabled[i];
            challenge.tracker.enabled = trackerEnabled;
            Time.timeScale = timeScale;
            Physics.SyncTransforms();
        }

        void OnDisable() => Clear();

        void LateUpdate()
        {
            if (!applied || !after || vanish == null || !challenge.Rules.NeedsVanish) return;
            for (int i = 0; i < vanish.figure.Length; i++) vanish.figure[i].enabled = false;
            if (playerSpot != null) playerSpot.enabled = false;
            if (glow != null) glow.SetActive(false);
        }
    }
}
