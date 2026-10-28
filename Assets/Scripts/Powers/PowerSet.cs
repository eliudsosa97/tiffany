using System;

namespace ElVelador
{
    public sealed class PowerSet
    {
        private int unlocked;

        public int Count
        {
            get
            {
                int count = 0;
                for (int bits = unlocked; bits != 0; bits &= bits - 1)
                {
                    count++;
                }
                return count;
            }
        }

        public bool Has(Power power) => (unlocked & ToMask(power)) != 0;
        public void Unlock(Power power) => unlocked |= ToMask(power);

        private static int ToMask(Power power)
        {
            if (power < Power.Light || power > Power.Manifest)
            {
                throw new ArgumentOutOfRangeException(nameof(power));
            }

            return 1 << (int)power;
        }

        public static Power ForMemory(int memoryId)
        {
            if (memoryId < 1 || memoryId > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(memoryId));
            }

            return (Power)(memoryId - 1);
        }

        public static bool TryParse(string name, out Power power)
        {
            switch (name)
            {
                case nameof(Power.Light): power = Power.Light; return true;
                case nameof(Power.Candles): power = Power.Candles; return true;
                case nameof(Power.Poltergeist): power = Power.Poltergeist; return true;
                case nameof(Power.Vanish): power = Power.Vanish; return true;
                case nameof(Power.Manifest): power = Power.Manifest; return true;
                default: power = default; return false;
            }
        }
    }
}
