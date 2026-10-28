using System;
using System.Collections.Generic;
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
        public Volume echoVolume;
        public Camera viewCamera;
        public float searchRadius = 0.6f;

        readonly MemoryEcho echo = new MemoryEcho();
        MemoryProgress progress;
        Dictionary<Collider, MemorySpot> spotsByCollider;
        Collider[] overlaps;
        UniversalAdditionalCameraData cameraData;
        bool echoApplied;
        bool previousPostProcessing;
        float previousWeight;

        public EchoPlayer ActiveEcho { get; private set; }

        public bool IsFound(int memoryId) => progress.IsFound(memoryId);

        void Awake()
        {
            var order = new int[spots.Length];
            spotsByCollider = new Dictionary<Collider, MemorySpot>(spots.Length);
            overlaps = new Collider[32];
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null) continue;
                order[i] = spot.memoryId;
                spotsByCollider[spot.GetComponent<SphereCollider>()] = spot;
            }
            Array.Sort(order);
            progress = new MemoryProgress(order);
            if (viewCamera != null) cameraData = viewCamera.GetComponent<UniversalAdditionalCameraData>();
        }

        void Start() => GuideTyler();

        void Update()
        {
            if (GameInput.InteractPressed && !gameInterface.IsFrozen && !hiding.Hidden) CollectNearby();
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
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, overlaps,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                if (spotsByCollider.TryGetValue(overlaps[i], out var spot) && Collect(spot.memoryId)) return;
        }

        public bool Collect(int memoryId)
        {
            if (!progress.MarkFound(memoryId)) return false;
            bool hasPower = memoryId >= 1 && memoryId <= 5;
            if (hasPower) soul.Powers.Unlock(PowerSet.ForMemory(memoryId));
            if (RouteProgress.StartsReturn(memoryId) && soul.TryGetComponent<PlayerRespawn>(out var respawn)
                && respawn.witch != null) respawn.witch.BeginReturn();
            if (!gameInterface.MemoryFound(memoryId) && hasPower)
                gameInterface.ShowPowerNotice(PowerSet.ForMemory(memoryId));
            MemorySpot found = null;
            for (int i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null || spots[i].memoryId != memoryId) continue;
                found = spots[i];
                if (found.visual != null) found.visual.SetActive(false);
                break;
            }
            if (!PlayEcho(found)) echo.Start();
            ApplyEcho(echo.Weight, echo.Active);
            GuideTyler();
            Debug.Log($"{GameInfo.LogPrefix} Recuerdo {memoryId} encontrado");
            return true;
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
