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
        public const float CliffPathMaxSlopeDegrees = 8f;
        public const float TrainWidth = 2.8f;
        public const float TrainClearance = 3.5f;
        public const float TrainRampLength = 28f;
        public const float TrainSouthRampLength = 52f;
        public const float TrainNorthEnd = 148f;

        static readonly Vector3[] Clearings =
        {
            new Vector3(180f, 0f, 140f),
            new Vector3(180f, 1.5f, 70f),
            new Vector3(50f, 2.8f, -60f),
            new Vector3(70f, 4.2f, 0f),
            new Vector3(104f, 6f, 60f),
            new Vector3(36f, 0f, 84f),
            new Vector3(56f, 0f, 156f),
            new Vector3(120f, 0f, 170f)
        };

        static readonly Vector3[][] Paths =
        {
            GradedPath(0, new Vector2(180f, 122f), new Vector2(180f, 118f),
                new Vector2(183f, 111f), new Vector2(182f, 103f), new Vector2(180f, 92f), new Vector2(180f, 88f)),
            GradedPath(1, new Vector2(180f, 52f), new Vector2(180f, 48f),
                new Vector2(174f, 20f), new Vector2(160f, -26f), new Vector2(120f, -60f),
                new Vector2(72f, -60f), new Vector2(68f, -60f)),
            GradedPath(2, new Vector2(68f, -50f), new Vector2(72f, -50f),
                new Vector2(80f, -30f), new Vector2(80f, -22f), new Vector2(80f, -18f)),
            GradedPath(3, new Vector2(80f, 18f), new Vector2(80f, 22f),
                new Vector2(84f, 29f), new Vector2(92f, 31f), new Vector2(96f, 38f), new Vector2(96f, 42f)),
            new[] { new Vector3(86f, 6f, 46f), new Vector3(82f, 6f, 46f),
                new Vector3(58f, 0f, 96f), new Vector3(54f, 0f, 96f) },
            GradedPath(5, new Vector2(36f, 102f), new Vector2(36f, 106f),
                new Vector2(38f, 110f), new Vector2(42f, 112f), new Vector2(44f, 116f), new Vector2(44f, 130f),
                new Vector2(44f, 134f), new Vector2(44f, 138f)),
            GradedPath(6, new Vector2(74f, 160f), new Vector2(78f, 160f),
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
            position = new Vector3(150f, 0f, Clearings[6].z);
            yawDegrees = 90f;
            width = 8f;
        }

        public static float Slope(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float horizontal = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            return Mathf.Atan2(Mathf.Abs(delta.y), horizontal) * Mathf.Rad2Deg;
        }

        static Vector3[] GradedPath(int from, params Vector2[] points)
        {
            var path = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                path[i] = new Vector3(points[i].x, PathPlaneHeight(from, points[i].x, points[i].y), points[i].y);
            return path;
        }

        public static float PathPlaneHeight(int from, float x, float z)
        {
            Vector3 a = Clearing(from), b = Clearing(from + 1);
            var direction = new Vector2(b.x - a.x, b.z - a.z);
            float distance = direction.magnitude;
            direction /= distance;
            float edge = ClearingSize * 0.5f * (Mathf.Abs(direction.x) + Mathf.Abs(direction.y));
            float along = Vector2.Dot(new Vector2(x - a.x, z - a.z), direction);
            return Mathf.Lerp(a.y, b.y, Mathf.Clamp01((along - edge) / (distance - edge * 2f)));
        }
    }
}
