using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class PlayerLight : MonoBehaviour
    {
        public Light spot;
        public Light baseGlow;
        public Light burstLight;
        public Transform visual;
        public AudioSource flashAudio;
        public AudioClip chargeClip, burstClip;
        public Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
        public float bodyGlowBoost;
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
        public FlashCharge Charge { get; private set; } = new FlashCharge();

        private PlayerSoul soul;
        private PlayerHiding hiding;
        private PlayerController movement;
        private Vector3 visualPosition;
        private float burstIntensity, pulseElapsed;
        private bool pulseActive, chargeAudioPlaying;
        private float baseIntensity;
        private float baseRange;
        private float baseAngle;
        private float baseInnerAngle;
        private static readonly int RimIntensity = Shader.PropertyToID("_RimIntensity");
        private MaterialPropertyBlock bodyProperties;
        private float[] baseBodyGlow, appliedBodyGlow;
        private float bodyGlow = 1f, bodyGlowVelocity;
        private RaycastHit[] hits;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            hiding = GetComponent<PlayerHiding>();
            if (visual != null) visualPosition = visual.localPosition;
            if (burstLight != null)
            {
                burstIntensity = burstLight.intensity;
                burstLight.enabled = false;
            }
            baseIntensity = spot.intensity;
            baseRange = spot.range;
            baseAngle = spot.spotAngle;
            baseInnerAngle = spot.innerSpotAngle;
            bodyProperties = new MaterialPropertyBlock();
            baseBodyGlow = new float[bodyRenderers.Length];
            appliedBodyGlow = new float[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var material = bodyRenderers[i] != null ? bodyRenderers[i].sharedMaterial : null;
                if (material != null && material.HasProperty(RimIntensity))
                    baseBodyGlow[i] = appliedBodyGlow[i] = material.GetFloat(RimIntensity);
            }
            hits = new RaycastHit[32];
        }

        private void Start()
        {
            movement = GetComponent<PlayerController>();
            State = new SoulLight(soul.Powers, soul.Energy);
            if (startOn) Toggle();
        }

        private void Update()
        {
            bool hidden = hiding != null && hiding.Hidden;
            State.SetHidden(hidden);
            State.Tick(Time.deltaTime);
            if (Time.timeScale > 0f)
            {
                bool wasCharging = Charge.Charging;
                bool canCharge = !hidden && soul.Powers.Has(Power.Light) && soul.Energy.Current >= SoulCosts.Flash;
                var result = Charge.Tick(Time.deltaTime, State.CanReadInput(Time.timeScale) && GameInput.LightHeld, canCharge);
                if (Charge.Charging && !wasCharging) Debug.Log($"{GameInfo.LogPrefix} Destello: carga");
                if (result == FlashChargeResult.Toggle) Toggle();
                if (result == FlashChargeResult.Cancelled || (wasCharging && !Charge.Charging && result != FlashChargeResult.Burst))
                    Debug.Log($"{GameInfo.LogPrefix} Destello: cancelado");
                if (result == FlashChargeResult.Burst) TryBurst();
                if (movement != null) movement.speedScale = Charge.MoveScale;
            }
            UpdateChargeAudio(Time.timeScale > 0f && Charge.Charging);
            UpdatePulse(Time.deltaTime, hidden);

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
            if (baseGlow != null) baseGlow.enabled = !hidden && visibility > 0f;
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
            float glow = SoulLight.BodyGlow(hidden, lightVisible, State.IsFlashing, size, bodyGlowBoost, flashBoost);
            if (!hidden && Charge.Charging) glow += Charge.Progress * flashBoost;
            ApplyBodyGlow(glow);

            if (witch != null)
            {
                witch.playerLightOn = visible;
                witch.playerLightPosition = LitPoint;
            }
        }

        private void LateUpdate()
        {
            if (visual != null) visual.localPosition = visualPosition + Vector3.up * Charge.FloatOffset;
        }

        private void OnDisable()
        {
            UpdateChargeAudio(false);
            if (flashAudio != null) flashAudio.Stop();
            if (burstLight != null) burstLight.enabled = false;
            if (movement != null) movement.speedScale = 1f;
            if (visual != null) visual.localPosition = visualPosition;
            pulseActive = false;
            Charge = new FlashCharge();
        }

        private void UpdateChargeAudio(bool charging)
        {
            if (flashAudio == null || chargeAudioPlaying == charging) return;
            chargeAudioPlaying = charging;
            if (!charging) { flashAudio.Stop(); flashAudio.loop = false; return; }
            flashAudio.clip = chargeClip;
            flashAudio.loop = true;
            if (chargeClip != null) flashAudio.Play();
        }

        private void UpdatePulse(float dt, bool hidden)
        {
            if (burstLight == null) return;
            if (hidden) pulseActive = false;
            if (!pulseActive) { burstLight.enabled = false; return; }
            pulseElapsed += dt;
            float strength = pulseElapsed <= FlashCharge.PulseRise
                ? Mathf.SmoothStep(0f, 1f, pulseElapsed / FlashCharge.PulseRise)
                : Mathf.SmoothStep(1f, 0f, (pulseElapsed - FlashCharge.PulseRise) / FlashCharge.PulseFade);
            burstLight.intensity = burstIntensity * strength;
            burstLight.range = Mathf.Max(0.01f, FlashCharge.BurstRadius * strength);
            burstLight.enabled = strength > 0f;
            pulseActive = pulseElapsed < FlashCharge.PulseRise + FlashCharge.PulseFade;
        }

        private void ApplyBodyGlow(float target)
        {
            bool immediate = target == 0f || bodyGlow == 0f;
            if (immediate) { bodyGlow = target; bodyGlowVelocity = 0f; }
            else if (Time.deltaTime > 0f) bodyGlow = Mathf.SmoothDamp(bodyGlow, target, ref bodyGlowVelocity, 0.15f);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                float value = baseBodyGlow[i] * bodyGlow;
                if (bodyRenderers[i] == null || value == appliedBodyGlow[i]
                    || (!immediate && Mathf.Abs(value - appliedBodyGlow[i]) <= 0.01f)) continue;
                bodyRenderers[i].GetPropertyBlock(bodyProperties);
                bodyProperties.SetFloat(RimIntensity, value);
                bodyRenderers[i].SetPropertyBlock(bodyProperties);
                appliedBodyGlow[i] = value;
            }
        }

        public void Toggle()
        {
            State.SetHidden(hiding != null && hiding.Hidden);
            State.Toggle();
        }

        public bool TryBurst()
        {
            State.SetHidden(hiding != null && hiding.Hidden);
            bool grounded = witch != null && witch.Decision.Form == WitchForm.Grounded
                && witch.Decision.State != WitchState.Caught;
            bool inRange = witch != null && FlashCharge.InBurst(transform.position,
                witch.transform.position, FlashCharge.BurstRadius);
            bool lineOfSight = grounded && inRange && HasFlashLineOfSight();
            if (!State.TryFlash(grounded, inRange, lineOfSight, out bool stuns)) return false;
            if (stuns) witch.Flash();
            pulseElapsed = 0f;
            pulseActive = true;
            UpdateChargeAudio(false);
            if (flashAudio != null && burstClip != null) flashAudio.PlayOneShot(burstClip);
            Debug.Log(stuns ? $"{GameInfo.LogPrefix} Destello: estallido, la bruja queda aturdida"
                : $"{GameInfo.LogPrefix} Destello: estallido");
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
