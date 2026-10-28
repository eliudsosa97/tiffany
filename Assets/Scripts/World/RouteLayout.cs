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
            new Vector3(0f, 0f, 0f),
            new Vector3(56f, 1.2f, 0f),
            new Vector3(112f, 2.4f, 0f),
            new Vector3(168f, 3.6f, 0f),
            new Vector3(216f, 5.4f, 56f),
            new Vector3(144f, 3.6f, 96f),
            new Vector3(72f, 1.8f, 80f),
            new Vector3(0f, 0f, 56f)
        };

        static readonly Vector3[][] Paths =
        {
            new[]
            {
                new Vector3(18f, 0f, 0f), new Vector3(28f, 0.6f, 3f), new Vector3(38f, 1.2f, 0f)
            },
            new[]
            {
                new Vector3(74f, 1.2f, 0f), new Vector3(84f, 1.8f, -3f), new Vector3(94f, 2.4f, 0f)
            },
            new[]
            {
                new Vector3(130f, 2.4f, 0f), new Vector3(140f, 3f, 3f), new Vector3(150f, 3.6f, 0f)
            },
            new[]
            {
                new Vector3(186f, 3.6f, 14f), new Vector3(192f, 4.5f, 28f), new Vector3(204f, 5.4f, 38f)
            },
            new[]
            {
                new Vector3(198f, 5.4f, 56f), new Vector3(190f, 4.4f, 70f),
                new Vector3(176f, 4.4f, 88f), new Vector3(162f, 3.6f, 96f)
            },
            new[]
            {
                new Vector3(126f, 3.6f, 96f), new Vector3(108f, 2.7f, 92f), new Vector3(90f, 1.8f, 80f)
            },
            new[]
            {
                new Vector3(54f, 1.8f, 80f), new Vector3(36f, 0.9f, 72f), new Vector3(18f, 0f, 56f)
            }
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

        public static void RiverSpan(out Vector3 start, out Vector3 end)
        {
            start = Paths[4][1];
            end = Paths[4][2];
        }

        public static void Gate(out Vector3 position, out float yawDegrees, out float width)
        {
            position = (Clearings[0] + Clearings[7]) * 0.5f;
            // El ancho de la reja sigue su eje X local.
            yawDegrees = 0f;
            width = 8f;
        }

        public static float Slope(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float horizontal = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            return Mathf.Atan2(Mathf.Abs(delta.y), horizontal) * Mathf.Rad2Deg;
        }
    }
}
