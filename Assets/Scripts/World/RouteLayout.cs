using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class RouteLayout
    {
        public const int Count = 8;
        public const float ClearingSize = 36f;
        public const float PathWidth = 5f;
        public const float MaxSlopeDegrees = 6f;

        static readonly Vector3[] Clearings =
        {
            new Vector3(180f, 0f, 140f),
            new Vector3(180f, 1.5f, 70f),
            new Vector3(140f, 2.8f, 20f),
            new Vector3(70f, 4.2f, 0f),
            new Vector3(104f, 5.6f, 60f),
            new Vector3(36f, 3f, 84f),
            new Vector3(56f, 1.6f, 156f),
            new Vector3(120f, 0f, 170f)
        };

        static readonly Vector3[][] Paths =
        {
            GradedPath(0, -1, new Vector2(180f, 122f), new Vector2(180f, 118f),
                new Vector2(183f, 111f), new Vector2(182f, 103f), new Vector2(180f, 92f), new Vector2(180f, 88f)),
            GradedPath(1, -1, new Vector2(162f, 58f), new Vector2(158f, 58f),
                new Vector2(151f, 56f), new Vector2(146f, 50f), new Vector2(146f, 42f), new Vector2(146f, 38f)),
            GradedPath(2, -1, new Vector2(122f, 10f), new Vector2(118f, 10f),
                new Vector2(110f, 12f), new Vector2(92f, 8f), new Vector2(88f, 8f)),
            GradedPath(3, -1, new Vector2(80f, 18f), new Vector2(80f, 22f),
                new Vector2(84f, 29f), new Vector2(92f, 31f), new Vector2(96f, 38f), new Vector2(96f, 42f)),
            GradedPath(4, -1, new Vector2(86f, 60f), new Vector2(82f, 60f),
                new Vector2(74f, 62f), new Vector2(68f, 68f), new Vector2(58f, 70f), new Vector2(54f, 70f)),
            GradedPath(5, 5, new Vector2(36f, 102f), new Vector2(36f, 106f),
                new Vector2(38f, 110f), new Vector2(42f, 112f), new Vector2(44f, 116f), new Vector2(44f, 130f),
                new Vector2(44f, 134f), new Vector2(44f, 138f)),
            GradedPath(6, -1, new Vector2(74f, 160f), new Vector2(78f, 160f),
                new Vector2(84f, 162f), new Vector2(91f, 160f), new Vector2(98f, 160f), new Vector2(102f, 160f))
        };

        public static Vector3 Clearing(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Clearings[index];
        }

        public static Vector3[] Path(int from)
        {
            if (from < 0 || from >= Count - 1) throw new ArgumentOutOfRangeException(nameof(from));
            return (Vector3[])Paths[from].Clone();
        }

        public static Vector3[] Openings(int index)
        {
            Clearing(index);
            if (index == 0) return new[] { Paths[0][0] };
            var previous = Paths[index - 1];
            if (index == Count - 1) return new[] { previous[previous.Length - 1] };
            return new[] { previous[previous.Length - 1], Paths[index][0] };
        }

        public static void RiverSpan(out Vector3 start, out Vector3 end)
        {
            start = Paths[5][4];
            end = Paths[5][5];
        }

        public static void Gate(out Vector3 position, out float yawDegrees, out float width)
        {
            position = (Clearings[0] + Clearings[7]) * 0.5f;
            Vector3 direction = Clearings[0] - Clearings[7];
            yawDegrees = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Vector3 axis = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.right;
            width = ClearingSize * (Mathf.Abs(axis.x) + Mathf.Abs(axis.z));
        }

        public static float Slope(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float horizontal = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            return Mathf.Atan2(Mathf.Abs(delta.y), horizontal) * Mathf.Rad2Deg;
        }

        static Vector3[] GradedPath(int from, int flatSegment, params Vector2[] points)
        {
            float length = 0f;
            for (int i = 2; i < points.Length - 1; i++)
                if (i != flatSegment) length += Vector2.Distance(points[i - 1], points[i]);
            var path = new Vector3[points.Length];
            float distance = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                if (i > 1 && i < points.Length - 1 && i != flatSegment)
                    distance += Vector2.Distance(points[i - 1], points[i]);
                path[i] = new Vector3(points[i].x, Mathf.Lerp(Clearings[from].y, Clearings[from + 1].y, distance / length), points[i].y);
            }
            return path;
        }
    }
}
