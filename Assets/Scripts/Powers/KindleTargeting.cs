using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class KindleTargeting
    {
        public static int Nearest(Vector3 player, IReadOnlyList<Vector3> candles,
            IReadOnlyList<bool> eligible, float range)
        {
            if (range < 0f) return -1;
            int nearest = -1;
            float bestDistance = float.PositiveInfinity;
            float rangeSquared = range * range;
            for (int i = 0; i < candles.Count; i++)
            {
                if (!eligible[i]) continue;
                Vector3 offset = candles[i] - player;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance <= rangeSquared && distance < bestDistance)
                {
                    nearest = i;
                    bestDistance = distance;
                }
            }
            return nearest;
        }
    }
}
