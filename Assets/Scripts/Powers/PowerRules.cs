using System;

namespace TiffanyGame
{
    public static class PowerRules
    {
        public const float MoveRange = 12f;
        public const float KindleRange = 2f;

        public static bool CanUseLight(PowerSet powers, SoulEnergy energy)
        {
            if (powers == null)
            {
                throw new ArgumentNullException(nameof(powers));
            }
            if (energy == null)
            {
                throw new ArgumentNullException(nameof(energy));
            }

            return powers.Has(Power.Light) && !energy.IsEmpty;
        }

        public static bool TryFlash(PowerSet powers, SoulEnergy energy,
            bool witchGrounded, bool witchInCone, out bool stunsWitch)
        {
            stunsWitch = false;
            if (!CanUseLight(powers, energy) || !energy.TrySpend(SoulCosts.Flash))
            {
                return false;
            }

            stunsWitch = witchGrounded && witchInCone;
            return true;
        }

        public static bool TryLightCandle(PowerSet powers, SoulEnergy energy, Candle candle, bool raining = false)
        {
            if (powers == null)
            {
                throw new ArgumentNullException(nameof(powers));
            }
            if (energy == null)
            {
                throw new ArgumentNullException(nameof(energy));
            }
            if (candle == null)
            {
                throw new ArgumentNullException(nameof(candle));
            }
            if (raining || !powers.Has(Power.Candles) || candle.Kind != CandleKind.Kindled || candle.IsLit
                || !energy.TrySpend(SoulCosts.LightCandle))
            {
                return false;
            }

            return candle.TryLight(raining);
        }

        public static bool TryMove(PowerSet powers, SoulEnergy energy, Throwable target,
            float distance, bool visibleInLight, bool raining, out float noiseRadius)
        {
            noiseRadius = 0f;
            if (powers == null)
            {
                throw new ArgumentNullException(nameof(powers));
            }
            if (energy == null)
            {
                throw new ArgumentNullException(nameof(energy));
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (!powers.Has(Power.Poltergeist) || !(distance >= 0f && distance <= MoveRange)
                || !visibleInLight || target.Used || !energy.TrySpend(SoulCosts.MoveObject))
            {
                return false;
            }

            target.Used = true;
            noiseRadius = Throwable.NoiseRadius(target.Kind) * (raining ? NoiseModel.RainMultiplier : 1f);
            return true;
        }
    }
}
