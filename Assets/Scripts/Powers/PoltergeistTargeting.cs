using UnityEngine;

namespace TiffanyGame
{
    public static class PoltergeistTargeting
    {
        public static bool InSpot(Vector3 origin, Vector3 forward, float range, float spotAngle, Vector3 point)
        {
            if (forward.sqrMagnitude == 0f || range < 0f) return false;
            Vector3 offset = point - origin;
            float distance = offset.magnitude;
            if (distance > range && !Mathf.Approximately(distance, range)) return false;
            if (offset.sqrMagnitude == 0f) return true;
            float angle = Vector3.Angle(forward, offset);
            float halfAngle = spotAngle * 0.5f;
            return angle <= halfAngle || Mathf.Approximately(angle, halfAngle);
        }

        public static float AimAngle(Vector3 from, Vector3 forward, Vector3 target)
        {
            Vector3 offset = target - from;
            offset.y = 0f;
            if (offset.sqrMagnitude == 0f) return 0f;
            forward.y = 0f;
            return forward.sqrMagnitude == 0f ? 180f : Vector3.Angle(forward, offset);
        }

        public static bool IsBetter(float angle, float distance, float bestAngle, float bestDistance)
        {
            return Mathf.Approximately(angle, bestAngle) ? distance < bestDistance : angle < bestAngle;
        }
    }
}
