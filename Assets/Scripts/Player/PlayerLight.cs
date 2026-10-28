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
        public float visibility = 1f;
        public float pitch = 35f;
        public float flashBoost = 4f;
        public float intensityScale = 1f;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public LayerMask occluderMask = Physics.DefaultRaycastLayers;

        public SoulLight State { get; private set; }
        public Vector3 LitPoint { get; private set; }

        private PlayerSoul soul;
        private PlayerHiding hiding;
        private float baseIntensity;
        private float baseRange;
        private float baseAngle;
        private float baseInnerAngle;
        private Vector3 baseGlowScale;
        private RaycastHit[] hits;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            hiding = GetComponent<PlayerHiding>();
            baseIntensity = spot.intensity;
            baseRange = spot.range;
            baseAngle = spot.spotAngle;
            baseInnerAngle = spot.innerSpotAngle;
            baseGlowScale = glow.localScale;
            hits = new RaycastHit[32];
        }

        private void Start()
        {
            State = new SoulLight(soul.Powers, soul.Energy);
            if (startOn) Toggle();
        }

        private void Update()
        {
            State.SetHidden(hiding != null && hiding.Hidden);
            State.Tick(Time.deltaTime);
            if (State.CanReadInput(Time.timeScale))
            {
                if (GameInput.LightTogglePressed) Toggle();
                if (GameInput.FlashPressed) TryFlash();
            }

            Vector3 normal = CastGround(transform.position + Vector3.up, Vector3.down, Mathf.Infinity, out var ground)
                ? ground.normal : Vector3.up;
            Vector3 direction = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            if (direction.sqrMagnitude == 0f) direction = Vector3.ProjectOnPlane(transform.up, normal).normalized;
            spot.transform.rotation = Quaternion.LookRotation(direction, normal) * Quaternion.Euler(pitch, 0f, 0f);
            LitPoint = CastGround(spot.transform.position, spot.transform.forward, spot.range, out var lit)
                ? lit.point : transform.position + direction * (spot.range * 0.5f);

            bool visible = State.Visible;
            bool lightVisible = visible && visibility > 0f;
            spot.enabled = lightVisible;
            float size = SoulLight.Size(soul.Energy.Current);
            if (visible)
            {
                float shrink = Mathf.Lerp(0.7f, 1f, (size - SoulLight.MinSize) / (1f - SoulLight.MinSize));
                spot.spotAngle = baseAngle * shrink;
                spot.innerSpotAngle = baseInnerAngle * shrink;
                spot.range = baseRange * size;
                spot.intensity = baseIntensity * SoulLight.Flicker(soul.Energy.Current, Time.time)
                    * (State.IsFlashing ? flashBoost : 1f) * visibility * intensityScale;
            }
            if (glow.gameObject.activeSelf != lightVisible) glow.gameObject.SetActive(lightVisible);
            glow.localScale = baseGlowScale * size * visibility;

            if (witch != null)
            {
                witch.playerLightOn = visible;
                witch.playerLightPosition = LitPoint;
            }
        }

        public void Toggle()
        {
            State.SetHidden(hiding != null && hiding.Hidden);
            State.Toggle();
        }

        public bool TryFlash()
        {
            State.SetHidden(hiding != null && hiding.Hidden);
            bool grounded = witch != null && witch.Decision.Form == WitchForm.Grounded
                && witch.Decision.State != WitchState.Caught;
            bool inCone = witch != null && SoulLight.InCone(transform.position, transform.forward,
                witch.transform.position, baseRange, baseAngle * 0.5f);
            bool lineOfSight = grounded && inCone && HasFlashLineOfSight();
            if (!State.TryFlash(grounded, inCone, lineOfSight, out bool stuns)) return false;
            if (stuns) witch.Flash();
            Debug.Log(stuns ? $"{GameInfo.LogPrefix} Destello, la bruja queda aturdida" : $"{GameInfo.LogPrefix} Destello");
            return true;
        }

        private bool HasFlashLineOfSight()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 direction = witch.transform.position + Vector3.up * 1.2f - origin;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, direction.magnitude,
                occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hitTransform = hits[i].collider.transform;
                if (!hitTransform.IsChildOf(transform) && !hitTransform.IsChildOf(witch.transform)) return false;
            }
            return count < hits.Length;
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
