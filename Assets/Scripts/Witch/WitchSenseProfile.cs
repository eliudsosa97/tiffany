using System;

namespace TiffanyGame
{
    [Serializable]
    public sealed class WitchSenseProfile
    {
        public float OverheadSightRadius = 6f;
        public float LightSightDistance = 45f;
        public float DarkSightDistance = 3f;
        public float LitSightDistance = 15f;
        public float SmellDistance = 2.5f;
    }
}
