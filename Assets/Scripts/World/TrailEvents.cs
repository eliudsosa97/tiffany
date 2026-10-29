namespace TiffanyGame
{
    public enum TrailEvent { None, PhantomSteps, BranchSnap, SkyCrossing, TylerGrowl }
    public sealed class TrailEvents
    {
        public const float FirstDelay = 10f, RetryDelay = 4f, ChaseCooldown = 12f, WitchNear = 20f;
        public const float WitchFlyingNear = 45f;
        public const float FirstInterval = 28f, SecondInterval = 40f, ThirdInterval = 34f, FourthInterval = 46f;
        private float remaining = FirstDelay, cooldown;
        private int interval;
        private TrailEvent next = TrailEvent.PhantomSteps, last;
        public TrailEvent Tick(float dt, bool onTrail, bool chase, bool inChallenge, float witchDistance,
            bool tylerNear, bool witchFlyingNear)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return TrailEvent.None;
            cooldown = chase || inChallenge ? ChaseCooldown : System.Math.Max(0f, cooldown - dt);
            if (!onTrail) return TrailEvent.None;
            remaining -= dt;
            if (remaining > 0f) return TrailEvent.None;
            if (chase || inChallenge || cooldown > 0f || witchDistance < WitchNear)
            {
                remaining = RetryDelay;
                return TrailEvent.None;
            }
            TrailEvent result;
            do
            {
                result = next;
                next = next == TrailEvent.TylerGrowl ? TrailEvent.PhantomSteps : next + 1;
            } while (result == last || (result == TrailEvent.TylerGrowl && !tylerNear)
                || (result == TrailEvent.SkyCrossing && witchFlyingNear));
            remaining = interval switch
            {
                0 => FirstInterval, 1 => SecondInterval, 2 => ThirdInterval, _ => FourthInterval
            };
            interval = (interval + 1) % 4;
            last = result;
            return result;
        }
    }
}
