using System;

namespace TiffanyGame
{
    [Serializable]
    public sealed class WitchProfile
    {
        public bool CanLand = true;
        public bool CanSprint = true;
        public bool UsesVoiceLure = true;
        public bool GroundChase;
        public float FlySpeed = 6f;
        public float FlightHeightOffset;
        public float StalkSpeed = 1.2f;
        public float FollowSpeed = 2.5f;
        public float SprintSpeed = 9f;
        public float SprintDuration = 1.2f;
        public float WindUpDuration = 0.8f;
        public float RecoverDuration = 0.8f;
        public float SprintTriggerDistance = 10f;
        public float CatchDistance = 1.2f;
        public float StunDuration = 2f;
        public float LoseTrackTime = 8f;
        public float LandingDuration = 1.5f;
        public float TakeOffDuration = 1.5f;
        public float LandingDistance = 12f;
        public float ArriveDistance = 1f;
        public float PlayerNoiseMatch = 1f;
        public float ClearNoiseDistance = 10f;
        public float ClearLightDistance = 20f;
        public float SuspicionDuration = 3f;
        public float HideInspectionDuration = 3f;
        public float HideRetreatDistance = 6f;
        public float RoamMinDelay = 45f;
        public float RoamMaxDelay = 60f;
        public float FarDistance = 25f;
        public float RoamMinDistance = 8f;
        public float RoamMaxDistance = 12f;
    }
}
