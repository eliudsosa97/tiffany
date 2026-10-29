using UnityEngine;

namespace TiffanyGame
{
    public struct WitchSenseInputs
    {
        public Vector3 WitchPosition;
        public WitchForm Form;
        public bool FireLit;
        public Vector3 PlayerPosition;
        public bool PlayerUnderCover;
        public bool LineOfSight;
        public bool PlayerLightOn;
        public Vector3 PlayerLightPosition;
        public bool PlayerInSafeZone;
        public bool PlayerHidden;
        public bool PlayerVanished;
        public bool Flashed;
        public bool Raining;
        public bool HearingMuted;
        public Vector3 PlayerForward;
        public bool HasNextClearing;
        public Vector3 NextClearingPosition;
        public bool HasFamilyCandle;
        public Vector3 FamilyCandlePosition;
        public float FamilyCandleRadius;
    }
}
