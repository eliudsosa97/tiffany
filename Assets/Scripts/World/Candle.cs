using UnityEngine;

namespace ElVelador
{
    public sealed class Candle
    {
        public const float LitDuration = 90f;
        public const float RainBurnMultiplier = 2f;

        public CandleKind Kind { get; }
        public Vector3 Position { get; }
        public float Remaining { get; private set; }
        public bool IsLit => Remaining > 0f;
        public bool IsSafeZone => IsLit;

        public Candle(CandleKind kind, Vector3 position, bool lit = false)
        {
            Kind = kind;
            Position = position;
            Remaining = kind == CandleKind.Family ? float.PositiveInfinity : lit ? LitDuration : 0f;
        }

        public bool TryLight()
        {
            if (IsLit)
            {
                return false;
            }

            Remaining = LitDuration;
            return true;
        }

        public void Tick(float dt, bool raining)
        {
            if (Kind == CandleKind.Kindled && IsLit && dt > 0f)
            {
                Remaining = Mathf.Max(0f, Remaining - dt * (raining ? RainBurnMultiplier : 1f));
            }
        }
    }
}
