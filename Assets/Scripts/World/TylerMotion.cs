using UnityEngine;

namespace TiffanyGame
{
    public static class TylerMotion
    {
        public const float FollowDistance = 2f;
        public const float GuideLead = 5f;
        public const float StillSpeed = 0.1f;
        public const float SpeedMargin = 0.5f;

        public static bool IsStill(Vector3 previous, Vector3 current, float dt)
        {
            if (dt <= 0f) return true;
            Vector3 velocity = (current - previous) / dt;
            velocity.y = 0f;
            return velocity.sqrMagnitude < StillSpeed * StillSpeed;
        }

        public static Vector3 FollowTarget(Vector3 tyler, Vector3 player)
        {
            Vector3 offset = tyler - player;
            offset.y = 0f;
            float distance = offset.magnitude;
            return distance <= FollowDistance ? tyler : player + offset * (FollowDistance / distance);
        }

        public static Vector3 GuideTarget(Vector3 tyler, Vector3 player, Vector3 memory)
        {
            Vector3 offset = memory - player;
            offset.y = 0f;
            return player + Vector3.ClampMagnitude(offset, GuideLead);
        }

        public static float MoveSpeed(float distanceToTarget)
        {
            return distanceToTarget <= 4f ? PlayerMotion.WalkSpeed : PlayerMotion.RunSpeed + SpeedMargin;
        }
    }
}
