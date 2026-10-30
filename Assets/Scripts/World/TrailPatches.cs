using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class TrailPatches
    {
        public const float Clearance = 6f;
        public const float HideEdgeDistance = 3f;
        public const float ApproachRadius = 2.3f;
        public const float FloorLift = 0.03f;

        static readonly Patch[] Layout =
        {
            new Patch(0, 25f, 6f, GroundType.Leaves, 1),
            new Patch(1, 20f, 6f, GroundType.Branches, -1),
            new Patch(2, 31.5f, 6f, GroundType.Leaves, 1),
            new Patch(3, 16.3f, 4f, GroundType.Branches, 1),
            new Patch(4, 2f, 4f, GroundType.Leaves, -1)
        };

        public static int Count => Layout.Length;
        public static Patch Get(int index) => Layout[index];

        public static int CountForPath(int from)
        {
            int count = 0;
            foreach (var patch in Layout) if (patch.From == from) count++;
            return count;
        }

        public static bool Reserves(Vector2 point, float radius)
        {
            foreach (var patch in Layout)
                if (RouteTerrain.SegmentDistance(point, Xz(patch.Center), Xz(patch.HidePosition))
                    < ApproachRadius + radius) return true;
            return false;
        }

        public readonly struct Patch
        {
            public readonly int From, HideSide;
            public readonly float Distance, Length;
            public readonly GroundType Type;
            public readonly Vector3 Center, Forward;
            public float Width => RouteLayout.PathWidth;
            public Vector3 Right => Vector3.Cross(Vector3.up, Forward);
            public Vector3 Start => Center - Forward * (Length * 0.5f);
            public Vector3 End => Center + Forward * (Length * 0.5f);
            public Vector3 HidePosition => Center + Right * (HideSide * (Width * 0.5f + HideEdgeDistance));

            public Patch(int from, float distance, float length, GroundType type, int hideSide)
            {
                From = from;
                Distance = distance;
                Length = length;
                Type = type;
                HideSide = hideSide;
                var points = RouteLayout.Path(from);
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(points[i] - points[i - 1], Vector3.up);
                    float segment = delta.magnitude;
                    if (distance <= segment)
                    {
                        Center = Vector3.Lerp(points[i - 1], points[i], distance / segment);
                        Forward = delta / segment;
                        return;
                    }
                    distance -= segment;
                }
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
    }
}
