using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerHiding : MonoBehaviour
    {
        public bool Hidden { get; private set; }
        public bool UsedInteractThisFrame { get; private set; }
        public float searchRadius = 0.6f;
        public Renderer[] body = System.Array.Empty<Renderer>();
        public GameInterface gameInterface;
        readonly Collider[] overlaps = new Collider[32];
        WitchController[] witches;
        PlayerController player;
        CharacterController controller;
        MemoryCollector memories;
        Dictionary<Collider, HideVolume> volumes;
        Vector3 entryPosition;

        public static Renderer[] BodyRenderers(GameObject player)
        {
            return System.Array.FindAll(player.GetComponentsInChildren<Renderer>(),
                renderer => !(renderer is ParticleSystemRenderer));
        }

        void Start() => Initialize();

        void Initialize()
        {
            if (player != null) return;
            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            memories = GetComponent<MemoryCollector>();
            witches = FindObjectsByType<WitchController>(FindObjectsSortMode.None);
            volumes = new Dictionary<Collider, HideVolume>();
            foreach (var volume in FindObjectsByType<HideVolume>(FindObjectsSortMode.None))
                volumes[volume.GetComponent<Collider>()] = volume;
        }

        void Update()
        {
            UsedInteractThisFrame = false;
            if ((gameInterface == null || !gameInterface.IsFrozen) && GameInput.InteractPressed)
            {
                var volume = FindNearby(out float hideDistance);
                if (!Hidden && memories != null && memories.FindNearby(out float memoryDistance) != null
                    && memoryDistance < hideDistance) volume = null;
                bool next = HidingRules.NextHidden(Hidden, true, volume != null);
                UsedInteractThisFrame = next != Hidden;
                if (next && !Hidden) Hide(volume);
                else if (!next && Hidden) Leave();
            }
            foreach (var witch in witches)
            {
                if (witch != null) witch.playerHidden = Hidden;
            }
        }

        public HideVolume FindNearby(out float distanceSquared)
        {
            HideVolume nearest = null;
            distanceSquared = float.PositiveInfinity;
            if (!isActiveAndEnabled) return null;
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, overlaps,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!volumes.TryGetValue(overlaps[i], out var volume)) continue;
                float distance = (overlaps[i].ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distance >= distanceSquared) continue;
                nearest = volume;
                distanceSquared = distance;
            }
            return nearest;
        }

        public void Hide(HideVolume volume)
        {
            if (Hidden || volume == null) return;
            Initialize();
            entryPosition = transform.position;
            SetHidden(true);
            MoveTo(volume.HidePoint);
            Debug.Log($"{GameInfo.LogPrefix} Escondida");
        }

        public void Leave()
        {
            if (!Hidden) return;
            MoveTo(entryPosition);
            SetHidden(false);
            Debug.Log($"{GameInfo.LogPrefix} Fuera del escondite");
        }

        void SetHidden(bool hidden)
        {
            Hidden = hidden;
            player.enabled = !hidden;
            foreach (var renderer in body)
            {
                if (renderer != null) renderer.enabled = !hidden;
            }
        }

        void MoveTo(Vector3 position)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }
    }
}
