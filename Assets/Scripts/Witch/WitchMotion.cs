using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class WitchMotion
    {
        public static Vector3 Fly(Vector3 position, Vector3 target, float speed, float dt)
        {
            dt = dt < 0f || float.IsNaN(dt) ? 0f : dt;
            return Vector3.MoveTowards(position, target, speed * dt);
        }

        public static Vector3 Vertical(Vector3 from, Vector3 to, float elapsed, float duration)
        {
            if (duration <= 0f) return to;
            return Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
        }

        public static Vector3 SprintStep(Vector3 position, Vector3 end, float speed, float dt, float freeDistance)
        {
            dt = dt < 0f || float.IsNaN(dt) ? 0f : dt;
            end.y = position.y;
            float distance = Mathf.Max(0f, Mathf.Min(speed * dt, freeDistance));
            return Vector3.MoveTowards(position, end, distance);
        }

        public static float CruiseHeight(IReadOnlyList<Vector3> route, Vector3 position, float fallback)
        {
            if (route == null || route.Count == 0) return fallback;
            float height = fallback;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 offset = route[i] - position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= closestDistance) continue;
                closestDistance = offset.sqrMagnitude;
                height = route[i].y;
            }
            return height;
        }

        public static Vector3 FarthestRoutePoint(IReadOnlyList<Vector3> route, Vector3 player, Vector3 fallback)
        {
            Vector3 result = fallback;
            float farthest = -1f;
            for (int i = 0; i < (route?.Count ?? 0); i++)
            {
                Vector3 offset = route[i] - player;
                offset.y = 0f;
                if (offset.sqrMagnitude <= farthest) continue;
                farthest = offset.sqrMagnitude;
                result = route[i];
            }
            return result;
        }

        public static bool ShouldRepath(Vector3 current, Vector3 next, float threshold)
        {
            current.y = next.y;
            return Vector3.Distance(current, next) > threshold;
        }
    }
}
