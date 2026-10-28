using System;

namespace TiffanyGame
{
    public sealed class Vanish
    {
        public const float Duration = 3f;

        public float Remaining { get; private set; }
        public bool IsActive => Remaining > 0f;

        public bool TryStart(PowerSet powers, SoulEnergy energy)
        {
            if (powers == null)
            {
                throw new ArgumentNullException(nameof(powers));
            }
            if (energy == null)
            {
                throw new ArgumentNullException(nameof(energy));
            }
            if (IsActive || !powers.Has(Power.Vanish) || !energy.TrySpend(SoulCosts.Vanish))
            {
                return false;
            }

            Remaining = Duration;
            return true;
        }

        public void Tick(float dt)
        {
            if (IsActive && dt > 0f)
            {
                Remaining = Math.Max(0f, Remaining - dt);
            }
        }
    }
}
