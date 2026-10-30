using System;

namespace TiffanyGame
{
    public sealed class SoulMeter
    {
        public const float LowThreshold = 0.2f;
        public const float ChangeSeconds = 0.7f;
        readonly SoulEnergy energy;
        float previous;
        float remaining;

        public float Fraction => energy.Current / SoulEnergy.Max;
        public bool IsUnlocked { get; private set; }
        public bool IsLow => Fraction <= LowThreshold;
        public int Change { get; private set; }

        public SoulMeter(SoulEnergy energy)
        {
            this.energy = energy;
            previous = energy.Current;
        }

        public void Unlock(Power power)
        {
            if (power == Power.Light) IsUnlocked = true;
        }

        public void Tick(float dt)
        {
            float current = energy.Current;
            if (current > previous || previous - current >= 1f)
            {
                Change = current > previous ? 1 : -1;
                remaining = ChangeSeconds;
            }
            else
            {
                remaining -= Math.Max(0f, dt);
                if (remaining <= 0f) Change = 0;
            }
            previous = current;
        }
    }
}
