using System;

namespace TiffanyGame
{
    [Serializable]
    public sealed class SaveData
    {
        public int version;
        public int[] memories;
        public int completedChallenges;
        public string candleId;
        public Chapter chapter;
        public float energy;
    }
}
