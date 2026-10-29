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
        public bool HasNoiseHide;
        public Vector3 NoiseHidePosition;
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
        public Vector3 PlayerForward;
        public bool HasNextClearing;
        public Vector3 NextClearingPosition;
        public bool HasFamilyCandle;
        public Vector3 FamilyCandlePosition;
        public float FamilyCandleRadius;
        public bool PlayerAtFamilyCandle;
    }
}
