using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerHiding : MonoBehaviour
    {
        public bool Hidden { get; private set; }
        public float searchRadius = 0.6f;
        public Renderer[] body = System.Array.Empty<Renderer>();
        public GameInterface gameInterface;
        readonly Collider[] overlaps = new Collider[32];
        WitchController[] witches;
        PlayerController player;
        CharacterController controller;
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
            witches = FindObjectsByType<WitchController>(FindObjectsSortMode.None);
        }

        void Update()
        {
            if ((gameInterface == null || !gameInterface.IsFrozen) && GameInput.InteractPressed)
            {
                HideVolume volume = null;
                int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, overlaps,
                    Physics.AllLayers, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    if (overlaps[i].TryGetComponent(out volume)) break;
                }
                bool next = HidingRules.NextHidden(Hidden, true, volume != null);
                if (next && !Hidden) Hide(volume);
                else if (!next && Hidden) Leave();
            }
            foreach (var witch in witches)
            {
                if (witch != null) witch.playerHidden = Hidden;
            }
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
