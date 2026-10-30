using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiffanyGame
{
    public sealed class MemoryCollector : MonoBehaviour
    {
        public PlayerSoul soul;
        public PlayerHiding hiding;
        public GameInterface gameInterface;
        public TylerController tyler;
        public MemorySpot[] spots = Array.Empty<MemorySpot>();
        public PowerChallenge[] challenges = Array.Empty<PowerChallenge>();
        public Volume echoVolume;
        public Camera viewCamera;
        public float searchRadius = 1.5f;

        readonly MemoryEcho echo = new MemoryEcho();
        MemoryProgress progress;
        SphereCollider[] spotColliders;
        UniversalAdditionalCameraData cameraData;
        bool echoApplied;
        bool previousPostProcessing;
        float previousWeight;

        public EchoPlayer ActiveEcho { get; private set; }
        public bool IsEchoActive => echo.Active;
        public MemoryProgress Progress => progress;
        public event Action<int> Collected;

        public bool IsFound(int memoryId) => progress.IsFound(memoryId);

        void Awake()
        {
            if (progress != null) return;
            var order = new int[spots.Length];
            spotColliders = new SphereCollider[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null) continue;
                order[i] = spot.memoryId;
                spotColliders[i] = spot.GetComponent<SphereCollider>();
            }
            progress = new MemoryProgress(order);
            if (viewCamera != null) cameraData = viewCamera.GetComponent<UniversalAdditionalCameraData>();
        }

        void Start() => GuideTyler();

        void Update()
        {
            if (GameInput.InteractPressed && !gameInterface.IsFrozen && !hiding.Hidden
                && !hiding.UsedInteractThisFrame) CollectNearby();
        }

        void LateUpdate()
        {
            if (ActiveEcho != null)
            {
                if (!ActiveEcho.IsPlaying) StopEcho();
                else echo.Tick(ActiveEcho.Elapsed - echo.Elapsed);
            }
            else echo.Tick(Time.deltaTime);
            ApplyEcho(echo.Weight, echo.Active);
        }

        void CollectNearby()
        {
            var spot = FindNearby(out _);
            if (spot != null) Collect(spot.memoryId);
        }

        public MemorySpot FindNearby(out float distanceSquared)
        {
            MemorySpot nearest = null;
            distanceSquared = float.PositiveInfinity;
            if (!isActiveAndEnabled) return null;
            float radiusSquared = searchRadius * searchRadius;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                var collider = spotColliders[i];
                if (spot == null || !spot.isActiveAndEnabled || collider == null || !collider.enabled
                    || !CanCollect(spot.memoryId)) continue;
                Vector3 center = collider.transform.TransformPoint(collider.center);
                float distance = (center - transform.position).sqrMagnitude;
                if (distance > radiusSquared || distance >= distanceSquared) continue;
                nearest = spot;
                distanceSquared = distance;
            }
            return nearest;
        }

        public bool CanCollect(int memoryId)
        {
            if (progress.IsFound(memoryId)) return false;
            int completed = 0;
            for (int i = 0; i < challenges.Length; i++)
                if (challenges[i] != null && challenges[i].Rules.Completed) completed |= 1 << (int)challenges[i].kind;
            return challenges.Length == 0 || RouteProgress.CanCollectMemory(memoryId, completed);
        }

        public bool Collect(int memoryId)
        {
            if (!CanCollect(memoryId) || !progress.MarkFound(memoryId)) return false;
            MemorySpot found = null;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null || spots[i].memoryId != memoryId) continue;
                found = spots[i];
                if (found.visual != null) found.visual.SetActive(false);
                break;
            }
            if (memoryId == MemoryProgress.BallId)
            {
                GuideTyler();
                if (tyler != null) tyler.Appear(transform.position);
                if (gameInterface != null) gameInterface.ShowTylerNotice();
                Debug.Log($"{GameInfo.LogPrefix} Tyler aparece");
                Collected?.Invoke(memoryId);
                return true;
            }
            bool hasPower = memoryId >= 1 && memoryId <= 5;
            if (hasPower) soul.Powers.Unlock(PowerSet.ForMemory(memoryId));
            if (RouteProgress.StartsReturn(memoryId) && soul.TryGetComponent<PlayerRespawn>(out var respawn)
                && respawn.witch != null) respawn.witch.BeginReturn();
            if (!gameInterface.MemoryFound(memoryId) && hasPower)
                gameInterface.ShowPowerNotice(PowerSet.ForMemory(memoryId));
            if (!PlayEcho(found)) echo.Start();
            ApplyEcho(echo.Weight, echo.Active);
            GuideTyler();
            Debug.Log($"{GameInfo.LogPrefix} Recuerdo {memoryId} encontrado");
            Collected?.Invoke(memoryId);
            return true;
        }

        public void RestoreFound()
        {
            StopEcho();
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null || !progress.IsFound(spot.memoryId)) continue;
                if (spot.visual != null) spot.visual.SetActive(false);
                if (spot.memoryId != MemoryProgress.BallId) gameInterface.MemoryFound(spot.memoryId, false);
            }
            if (progress.IsFound(MemoryProgress.BallId) && tyler != null) tyler.Appear(transform.position);
            GuideTyler();
        }

        public bool PlayEcho(MemorySpot spot)
        {
            StopEcho();
            if (spot == null || spot.echoPlayer == null || !spot.echoPlayer.Play()) return false;
            ActiveEcho = spot.echoPlayer;
            echo.Start(ActiveEcho.Data.duration);
            ApplyEcho(echo.Weight, echo.Active);
            return true;
        }

        public void StopEcho()
        {
            if (ActiveEcho != null) ActiveEcho.Stop();
            ActiveEcho = null;
            echo.Stop();
            ApplyEcho(0f, false);
        }

        void GuideTyler()
        {
            if (tyler == null) return;
            tyler.nextMemory = null;
            if (!progress.TryGetNext(out int id)) return;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null || spots[i].memoryId != id) continue;
                tyler.nextMemory = spots[i].transform;
                return;
            }
        }

        void ApplyEcho(float weight, bool active)
        {
            if (active && !echoApplied)
            {
                previousWeight = echoVolume != null ? echoVolume.weight : 0f;
                previousPostProcessing = cameraData != null && cameraData.renderPostProcessing;
                echoApplied = true;
            }
            if (active)
            {
                if (echoVolume != null) echoVolume.weight = weight;
                if (cameraData != null) cameraData.renderPostProcessing = true;
            }
            else if (echoApplied)
            {
                if (echoVolume != null) echoVolume.weight = previousWeight;
                if (cameraData != null) cameraData.renderPostProcessing = previousPostProcessing;
                echoApplied = false;
            }
        }

        void OnDisable() => StopEcho();
    }
}
