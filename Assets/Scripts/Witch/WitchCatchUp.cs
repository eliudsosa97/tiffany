using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchCatchUp
    {
        public const float RetryDelay = 1f;
        public const float PlacementSlack = 3f;
        public const float MaxPlacementAngle = 20f;
        private float farTime;

        public bool Hunting { get; private set; }

        public bool Tick(float dt, WitchProfile profile, in WitchPerception p, WitchState state)
        {
            bool protectedPlayer = p.PlayerInSafeZone || p.PlayerHidden || p.PlayerVanished;
            if (state != WitchState.Stalking || protectedPlayer) Hunting = false;
            bool far = profile.GroundChase && state == WitchState.Stalking && !protectedPlayer
                && WitchTargets.Distance(p.WitchPosition, p.PlayerPosition) > profile.CatchUpDistance;
            farTime = far ? farTime + dt : 0f;
            return farTime >= profile.CatchUpDelay;
        }

        public void Placed()
        {
            farTime = 0f;
            Hunting = true;
        }

        public void Retry(WitchProfile profile) => farTime = Mathf.Min(farTime, profile.CatchUpDelay - RetryDelay);

        public static bool AcceptsPlacement(Vector3 player, Vector3 wanted, Vector3 found, WitchProfile profile)
        {
            float distance = WitchTargets.Distance(found, player);
            if (distance <= profile.SprintTriggerDistance
                || distance > WitchTargets.Distance(wanted, player) + PlacementSlack) return false;
            return Vector3.Angle(WitchTargets.Direction(player, found), WitchTargets.Direction(player, wanted)) <= MaxPlacementAngle;
        }

        public static Vector3 PointAlongPath(Vector3[] corners, int count, float distance)
        {
            for (int i = 1; i < count; i++)
            {
                float segment = Vector3.Distance(corners[i - 1], corners[i]);
                if (distance <= segment) return Vector3.MoveTowards(corners[i - 1], corners[i], distance);
                distance -= segment;
            }
            return corners[count - 1];
        }
    }
}
