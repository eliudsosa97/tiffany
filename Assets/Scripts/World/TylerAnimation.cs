namespace TiffanyGame
{
    public static class TylerAnimation
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Sit = "Sit";
        public const string Whine = "Whine";
        public const string Growl = "Growl";
        public const string Bark = "Bark";
        public const string SpeedParameter = "Speed";
        public const float SitDelay = 4f;

        public static string StateFor(TylerReaction reaction, float speed, float idleSeconds)
        {
            if (reaction == TylerReaction.Whine) return Whine;
            if (reaction == TylerReaction.Growl) return Growl;
            if (speed > TylerMotion.StillSpeed) return Walk;
            return idleSeconds > SitDelay ? Sit : Idle;
        }
    }
}
