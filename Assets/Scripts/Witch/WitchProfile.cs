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
        public bool SearchEnabled;
        public bool AmbushEnabled;
        public bool CandleWatchEnabled;
        public bool BarkLureEnabled;
        public bool LastSeenEnabled;
        public float SearchDuration = 20f;
        public float SearchStartRadius = 3f;
        public float SearchRadiusGrowth = 3f;
        public float SearchAngleStep = 60f;
        public float SearchMaxRadius = 15f;
        public float AmbushMinDelay = 40f;
        public float AmbushMaxDelay = 70f;
        public float AmbushMinPlayerDistance = 25f;
        public float AmbushEdgeOffset = 8f;
        public float AmbushWait = 45f;
        public float CandleWatchRadius = 6f;
        public float CandleWatchMargin = 3f;
        public float CandleWatchAngleStep = 30f;
        public float CandleWatchLimit = 20f;
        public float CandleWatchCooldown = 60f;
        public float CandleWatchRetreatDistance = 6f;
        public float CandleWatchRetreatDuration = 6f;
        public float BarkLureMinDelay = 25f;
        public float BarkLureMaxDelay = 40f;
        public float BarkLureDistance = 15f;
        public float BarkLureTravelLimit = 15f;
        public float BarkLureDuration = 10f;
        public float LastSeenDuration = 20f;
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
        public float ReturnPlacementDistance = 18f;
        public float ReturnGraceDuration = 8f;
        public float CatchUpDistance = 20f;
        public float CatchUpDelay = 6f;
        public float CatchUpRespawnDistance = 20f;
        public float CatchUpRespawnDuration = 30f;
        public float CatchUpReappearDistance = 14f;
        public float CatchUpReappearMaxDistance = 18f;

        public WitchProfile Copy() => (WitchProfile)MemberwiseClone();
    }
}
