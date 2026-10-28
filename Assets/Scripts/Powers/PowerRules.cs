using System;

namespace ElVelador
{
    public static class PowerRules
    {
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
    }
}
