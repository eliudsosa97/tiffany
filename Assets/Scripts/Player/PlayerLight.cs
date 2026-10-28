using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class PlayerLight : MonoBehaviour
    {
        public Light spot;
        public Transform glow;
        public WitchController witch;
        public bool startOn;
        public float pitch = 35f;
        public float flashBoost = 4f;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;

        public SoulLight State { get; private set; }
        public Vector3 LitPoint { get; private set; }

        private PlayerSoul soul;
        private float baseIntensity;
        private float baseRange;
        private float baseAngle;
        private float baseInnerAngle;
        private Vector3 baseGlowScale;
        private RaycastHit[] hits;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            baseIntensity = spot.intensity;
            baseRange = spot.range;
            baseAngle = spot.spotAngle;
            baseInnerAngle = spot.innerSpotAngle;
            baseGlowScale = glow.localScale;
            hits = new RaycastHit[8];
        }

        private void Start()
        {
            State = new SoulLight(soul.Powers, soul.Energy);
            if (startOn) Toggle();
        }

        private void Update()
        {
            State.Tick(Time.deltaTime);
            if (GameInput.LightTogglePressed) Toggle();
            if (GameInput.FlashPressed) TryFlash();

            Vector3 normal = CastGround(transform.position + Vector3.up, Vector3.down, Mathf.Infinity, out var ground)
                ? ground.normal : Vector3.up;
            Vector3 direction = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            if (direction.sqrMagnitude == 0f) direction = Vector3.ProjectOnPlane(transform.up, normal).normalized;
            spot.transform.rotation = Quaternion.LookRotation(direction, normal) * Quaternion.Euler(pitch, 0f, 0f);
            LitPoint = CastGround(spot.transform.position, spot.transform.forward, spot.range, out var lit)
                ? lit.point : transform.position + direction * (spot.range * 0.5f);

            bool visible = State.Visible;
            spot.enabled = visible;
            float size = SoulLight.Size(soul.Energy.Current);
            if (visible)
            {
                float shrink = Mathf.Lerp(0.7f, 1f, (size - SoulLight.MinSize) / (1f - SoulLight.MinSize));
                spot.spotAngle = baseAngle * shrink;
                spot.innerSpotAngle = baseInnerAngle * shrink;
                spot.range = baseRange * size;
                spot.intensity = baseIntensity * SoulLight.Flicker(soul.Energy.Current, Time.time)
                    * (State.IsFlashing ? flashBoost : 1f);
            }
            if (glow.gameObject.activeSelf != visible) glow.gameObject.SetActive(visible);
            glow.localScale = baseGlowScale * size;

            if (witch != null)
            {
                witch.playerLightOn = visible;
                witch.playerLightPosition = LitPoint;
            }
        }

        public void Toggle() => State.Toggle();

        public bool TryFlash()
        {
            bool grounded = witch != null && witch.Decision.Form == WitchForm.Grounded
                && witch.Decision.State != WitchState.Caught;
            bool inCone = witch != null && SoulLight.InCone(transform.position, transform.forward,
                witch.transform.position, baseRange, baseAngle * 0.5f);
            if (!State.TryFlash(grounded, inCone, out bool stuns)) return false;
            if (stuns) witch.Flash();
            Debug.Log(stuns ? $"{GameInfo.LogPrefix} Destello, la bruja queda aturdida" : $"{GameInfo.LogPrefix} Destello");
            return true;
        }

        private bool CastGround(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, groundMask, QueryTriggerInteraction.Ignore);
            closest = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                closest = hit;
                nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
