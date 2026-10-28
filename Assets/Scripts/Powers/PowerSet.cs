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
    }
}
