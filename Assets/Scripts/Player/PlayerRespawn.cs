using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        public WitchController witch;
        public Image overlay;
        public Color fireColor = new Color(1f, 0.45f, 0.1f);
        public float sideOffset = 1f;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public bool Active => sequence.Active;
        public event System.Action Respawned;

        private readonly CaughtSequence sequence = new CaughtSequence();
        private PlayerSoul soul;
        private RespawnPoint respawn;
        private PlayerController movement;
        private PlayerHiding hiding;
        private CharacterController character;
        private PlayerLight playerLight;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            respawn = soul.Respawn;
            movement = GetComponent<PlayerController>();
            hiding = GetComponent<PlayerHiding>();
            character = GetComponent<CharacterController>();
            playerLight = GetComponent<PlayerLight>();
        }

        private void OnEnable() { if (witch != null) witch.Caught += Trigger; }
        private void OnDisable() { if (witch != null) witch.Caught -= Trigger; }

        public void Trigger()
        {
            if (Active) return;
            sequence.Begin();
            if (hiding != null)
            {
                if (hiding.Hidden) hiding.Leave();
                hiding.enabled = false;
            }
            if (movement != null) movement.enabled = false;
        }

        private void Update()
        {
            bool wasActive = Active;
            sequence.Tick(Time.deltaTime);
            if (overlay != null)
            {
                Color color = Color.Lerp(Color.black, fireColor, sequence.FireAmount);
                color.a = sequence.Overlay;
                overlay.color = color;
                if (overlay.gameObject.activeSelf != (color.a > 0f)) overlay.gameObject.SetActive(color.a > 0f);
            }
            if (sequence.RespawnDue)
            {
                RespawnAtCheckpoint();
                Debug.Log($"{GameInfo.LogPrefix} Atrapada: regreso a la veladora");
            }
            if (wasActive && !Active)
            {
                if (movement != null) movement.enabled = true;
                if (hiding != null) hiding.enabled = true;
            }
        }

        public void ReturnToCheckpoint()
        {
            sequence.Cancel();
            if (TryGetComponent(out VanishPower vanish)) vanish.Cancel();
            if (hiding != null && hiding.Hidden) hiding.Leave();
            RespawnAtCheckpoint();
            if (movement != null) movement.enabled = true;
            if (hiding != null) hiding.enabled = true;
            if (overlay != null) overlay.gameObject.SetActive(false);
        }

        void RespawnAtCheckpoint()
        {
            respawn.Respawn(soul.Energy);
            PlaceAtCheckpoint();
            if (witch != null) witch.ResetBrain(afterCapture: true);
            Respawned?.Invoke();
            if (playerLight != null && playerLight.State != null && playerLight.State.IsOn) playerLight.Toggle();
        }

        public void PlaceAtCheckpoint()
        {
            Vector3 position = respawn.Position + Vector3.back * sideOffset;
            bool wasEnabled = character != null && character.enabled;
            if (character != null) character.enabled = false;
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit,
                Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore)) position.y = hit.point.y;
            transform.position = position;
            Physics.SyncTransforms();
            if (character != null) character.enabled = wasEnabled;
        }
    }
}
