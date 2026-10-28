using System;
using System.Collections.Generic;

namespace ElVelador
{
    public sealed class PowerSet
    {
        private readonly HashSet<Power> unlocked = new HashSet<Power>();

        public int Count => unlocked.Count;
        public bool Has(Power power) => unlocked.Contains(power);
        public void Unlock(Power power) => unlocked.Add(power);

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
