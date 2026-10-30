using UnityEngine;

namespace TiffanyGame
{
    public enum FlashChargeResult { None, Toggle, Cancelled, Burst }

    public sealed class FlashCharge
    {
        public const float TapSeconds = 0.3f;
        public const float ChargeSeconds = 3f;
        public const float BurstRadius = 11f;
        public const float FloatHeight = 0.5f;
        public const float ChargeMoveScale = 0.15f;
        public const float PulseRise = 0.12f;
        public const float PulseFade = 0.6f;

        float heldSeconds, fallSeconds, fallHeight;
        bool wasHeld, eligible, burst;

        public bool Charging { get; private set; }
        public float Progress { get; private set; }
        public bool Attempting => wasHeld && !burst && heldSeconds >= TapSeconds;
        public float AttemptProgress => Mathf.InverseLerp(TapSeconds, ChargeSeconds, heldSeconds);
        public float FloatOffset { get; private set; }
        public float MoveScale => Charging ? ChargeMoveScale : 1f;

        public FlashChargeResult Tick(float dt, bool held, bool canCharge)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return FlashChargeResult.None;
            var result = FlashChargeResult.None;
            bool wasCharging = Charging;
            if (held)
            {
                if (!wasHeld) eligible = canCharge;
                eligible &= canCharge;
                heldSeconds = Mathf.Min(ChargeSeconds, heldSeconds + dt);
                Charging = eligible && !burst && heldSeconds >= TapSeconds;
                Progress = Charging ? Mathf.InverseLerp(TapSeconds, ChargeSeconds, heldSeconds) : 0f;
                if (Charging && heldSeconds >= ChargeSeconds)
                {
                    burst = true;
                    Charging = false;
                    result = FlashChargeResult.Burst;
                }
            }
            else
            {
                if (wasHeld && !burst)
                    result = heldSeconds < TapSeconds ? FlashChargeResult.Toggle
                        : eligible ? FlashChargeResult.Cancelled : FlashChargeResult.None;
                heldSeconds = Progress = 0f;
                Charging = eligible = burst = false;
            }

            if (Charging || result == FlashChargeResult.Burst)
            {
                FloatOffset = Mathf.SmoothStep(0f, FloatHeight, Progress);
                fallSeconds = 0f;
                fallHeight = FloatOffset;
            }
            else
            {
                if (wasCharging) { fallSeconds = 0f; fallHeight = FloatOffset; }
                fallSeconds = Mathf.Min(PulseFade, fallSeconds + dt);
                FloatOffset = Mathf.SmoothStep(fallHeight, 0f, fallSeconds / PulseFade);
            }
            wasHeld = held;
            return result;
        }

        public static bool InBurst(Vector3 player, Vector3 witch, float radius)
        {
            Vector3 offset = witch - player;
            offset.y = 0f;
            return radius >= 0f && offset.sqrMagnitude <= radius * radius;
        }
    }
}
