using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class CrossingLayout
    {
        public const float MaxWalkingSpacing = 0.85f;

        public static Vector3[] Create(Vector3 start, Vector3 end, int count, float maxSpacing = MaxWalkingSpacing)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var stones = new Vector3[count];
            for (int i = 0; i < count; i++)
                stones[i] = Vector3.Lerp(start, end, (i + 0.5f) / count);
            if (!IsWalkable(start, end, stones, maxSpacing))
                throw new ArgumentException($"{GameInfo.LogPrefix} El cruce necesita piedras más cercanas.");
            return stones;
        }

        public static bool IsWalkable(Vector3 start, Vector3 end, IReadOnlyList<Vector3> stones,
            float maxSpacing = MaxWalkingSpacing)
        {
            if (stones == null || stones.Count == 0 || !Finite(start) || !Finite(end) ||
                float.IsNaN(maxSpacing) || float.IsInfinity(maxSpacing) || maxSpacing <= 0f) return false;
            Vector3 previous = start;
            for (int i = 0; i <= stones.Count; i++)
            {
                Vector3 next = i == stones.Count ? end : stones[i];
                if (!Finite(next) || Vector3.Distance(previous, next) > maxSpacing) return false;
                previous = next;
            }
            return true;
        }

        static bool Finite(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
            !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }
}
