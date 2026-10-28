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
                    position.y = challenge.post.position.y;
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
