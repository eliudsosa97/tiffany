using System;
using UnityEngine;

namespace TiffanyGame
{
    internal static class WitchTargets
    {
        public static bool TryResolve(Func<Vector3, Vector3> resolve, Vector3 point, out Vector3 target)
        {
            target = resolve != null ? resolve(point) : point;
            return IsFinite(target.x) && IsFinite(target.y) && IsFinite(target.z);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float Distance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }

        public static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }
    }
}
