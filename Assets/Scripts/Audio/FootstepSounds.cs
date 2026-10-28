using System;

namespace TiffanyGame
{
    public sealed class FootstepSounds
    {
        readonly int[] counts;
        readonly int[] previous;
        readonly float[] volumes;

        public FootstepSounds(int[] counts, float slow = 0.35f, float walk = 0.6f, float run = 1f)
        {
            this.counts = counts;
            previous = new int[counts.Length];
            volumes = new[] { slow, walk, run };
        }

        public (GroundType Ground, int Index, float Volume, float Pitch) Choose(GroundType ground, Gait gait, float random)
        {
            if (counts[(int)ground] == 0) ground = GroundType.Dirt;
            int count = counts[(int)ground], last = previous[(int)ground] - 1;
            int choices = count > 1 && last >= 0 ? count - 1 : count;
            int index = choices == 0 ? -1 : Math.Min((int)(random * choices), choices - 1);
            if (count > 1 && last >= 0 && index >= last) index++;
            previous[(int)ground] = index + 1;
            return (ground, index, volumes[(int)gait], 0.95f + random * 0.1f);
        }
    }
}
