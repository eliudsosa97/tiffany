using UnityEngine;

namespace TiffanyGame
{
    public struct WitchPerception
    {
        public Vector3 WitchPosition;
        public bool SeesPlayerLight;
        public Vector3 PlayerLightPosition;
        public bool HeardNoise;
        public Vector3 NoisePosition;
        public bool SmellsPlayer;
        public bool SeesPlayer;
        public Vector3 PlayerPosition;
        public float DistanceToPlayer;
        public bool PlayerInSafeZone;
        public bool PlayerVanished;
        public bool PlayerHidden;
        public bool Flashed;
        public bool Raining;
        public bool HearingMuted;
        public bool InWatchPassage;
    }
}
