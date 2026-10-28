using System;

namespace TiffanyGame
{
    [Serializable]
    public sealed class SaveData
    {
        public int version;
        public int[] memories;
        public string candleId;
        public Chapter chapter;
        public float energy;
    }
}
