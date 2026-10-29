using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchCatchUp
    {
        public const float RetryDelay = 1f;
        private float farTime;

        public bool Tick(float dt, WitchProfile profile, in WitchPerception p, WitchState state)
        {
            bool far = profile.GroundChase && state == WitchState.Stalking
                && !p.PlayerInSafeZone && !p.PlayerHidden && !p.PlayerVanished
                && WitchTargets.Distance(p.WitchPosition, p.PlayerPosition) > profile.CatchUpDistance;
            farTime = far ? farTime + dt : 0f;
            return farTime >= profile.CatchUpDelay;
        }

        public void Reset() => farTime = 0f;

        public void Retry(WitchProfile profile) => farTime = Mathf.Min(farTime, profile.CatchUpDelay - RetryDelay);
    }
}
