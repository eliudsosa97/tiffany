using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class RouteChapters
    {
        public static int FindClearing(Vector3 position, Bounds[] bounds)
        {
            if (bounds == null) return -1;
            for (int i = 0; i < bounds.Length; i++)
            {
                if (bounds[i].size == Vector3.zero) continue;
                Vector3 min = bounds[i].min;
                Vector3 max = bounds[i].max;
                if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z) continue;
                return i;
            }
            return -1;
        }

        public static Chapter Resolve(Chapter current, int clearing)
        {
            if (current == Chapter.Return || clearing < 1 || clearing > 8) return current;
            var reached = (Chapter)Math.Min(clearing - 1, (int)Chapter.WitchHut);
            return (int)reached == (int)current + 1 ? reached : current;
        }

        public static bool CanVisitClearing(int lastVisited, int clearing) =>
            clearing >= 0 && clearing <= lastVisited + 1;
    }
}
