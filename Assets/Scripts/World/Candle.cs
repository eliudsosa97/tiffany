using UnityEngine;

namespace TiffanyGame
{
    public sealed class Candle
    {
        public const float LitDuration = 90f;

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

        public bool TryLight(bool raining = false)
        {
            if (raining || IsLit)
            {
                return false;
            }

            Remaining = LitDuration;
            return true;
        }

        public void Extinguish()
        {
            if (Kind == CandleKind.Kindled) Remaining = 0f;
        }

        public void Tick(float dt, bool raining)
        {
            if (raining)
            {
                Extinguish();
                return;
            }
            if (Kind == CandleKind.Kindled && IsLit && dt > 0f)
            {
                Remaining = Mathf.Max(0f, Remaining - dt);
            }
        }
    }
}
