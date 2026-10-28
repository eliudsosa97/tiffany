using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static partial class ForestBuilder
    {
        // BuildEdges se ejecuta antes de los hitos, escondites, recuerdos y veladoras de RouteBuilder.
        public static (Vector2 center, float radius)[] RouteExclusions(int index)
        {
            var zones = new List<(Vector2, float)>();
            void Add(float x, float z, float radius) => zones.Add((new Vector2(x, z), radius));
            foreach (Vector3 opening in RouteLayout.Openings(index))
            {
                Vector3 local = opening - RouteLayout.Clearing(index);
                int steps = Mathf.CeilToInt(local.magnitude);
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 point = local * ((float)i / steps);
                    Add(point.x, point.z, RouteLayout.PathWidth * 0.5f + 0.5f);
                }
            }
            Add(-6f, -8f, 3.5f);
            Add(0f, index == 0 ? -3.2f : index == 1 ? -1f : index == 2 ? -3f : 3f, 2f);
            switch (index)
            {
                case 0:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int z = 3; z <= 13; z += 5) Add(side * 9f, z, 3f);
                        Add(side * 13f, -11f, 3.3f);
                        Add(side * 5f, -16f, 3f);
                    }
                    Add(-9f, 5f, 2.3f);
                    Add(3f, -6f, 1f);
                    break;
                case 1:
                    for (int x = -10; x <= 10; x += 5) Add(x, 0f, 2.7f);
                    Add(-7f, 8f, 4.1f);
                    Add(7f, 8f, 4.1f);
                    Add(6f, -1.5f, 2.3f);
                    break;
                case 2:
                    for (int z = -12; z <= 12; z += 6) Add(0f, z, 4f);
                    Add(6f, -4f, 2.3f);
                    break;
                case 3: Add(0f, 5f, 9.5f); break;
                case 4: Add(0f, 5f, 6.5f); Add(-6f, 3f, 2.3f); break;
                case 5: Add(9f, 3f, 13f); break;
                case 6:
                    for (int x = -15; x <= 15; x += 5) Add(x, 0f, 4.8f);
                    break;
                case 7: Add(0f, 5f, 8.8f); Add(-4f, -0.7f, 3.5f); break;
            }
            return zones.ToArray();
        }

        public static Vector2[] RouteViewPoints(int index)
        {
            var points = new List<Vector2>
            {
                index == 7 ? new Vector2(-4f, -0.7f) : new Vector2(-6f, -8f),
                new Vector2(0f, index == 0 ? -3.2f : index == 1 ? -1f : index == 2 ? -3f : 3f)
            };
            var hides = new[] { new Vector2(-9f, 5f), new Vector2(6f, -1.5f), new Vector2(6f, -4f),
                new Vector2(-4.5f, 7f), new Vector2(-6f, 3f) };
            if (index < hides.Length)
            {
                points.Add(hides[index]);
                points.Add(hides[index] + Vector2.left * 1.2f);
            }
            return points.ToArray();
        }

        // La proyección debe coincidir con la pose y el lookAhead de FollowCamera.
        static bool KeepsView(Vector3 position, float radius, float height, Vector2[] points, Vector3 localViewDirection)
        {
            if (height == 0f || points == null) return true;
            var start = new Vector2(position.x, position.z);
            var projection = new Vector2(localViewDirection.x, localViewDirection.z) * (height / -localViewDirection.y);
            foreach (var point in points)
            {
                float t = Mathf.Clamp01(Vector2.Dot(point - start, projection) / projection.sqrMagnitude);
                if (Vector2.Distance(point, start + projection * t) < radius + FollowCamera.DefaultLookAhead) return false;
            }
            return true;
        }
    }
}
