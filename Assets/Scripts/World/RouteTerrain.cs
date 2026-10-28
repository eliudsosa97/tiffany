using UnityEngine;

namespace TiffanyGame
{
    public sealed class RouteTerrain
    {
        public const float Margin = 20f;
        public const float RiverWidth = 4f;
        public const float CrossingWidth = 1.6f;
        public const float RoadWidth = 8f;
        public const float CliffHeight = 6f;
        public const float RampLanding = 6f;
        public const float ForestTileSize = 32f;
        public const string ForestArea = "Forest";
        public const float ForestCost = 4f;
        public static readonly Vector2 LakeCenter = new Vector2(-32f, 44f);
        public static readonly Vector2 LakeRadius = new Vector2(65f, 52f);
        public static readonly Vector2 CliffFoot = new Vector2(75f, 94f);
        public static readonly Vector2 Plateau = new Vector2(77f, 94f);

        readonly Vector3[][] paths = new Vector3[RouteLayout.Count - 1][];
        readonly Vector3[][] openings = new Vector3[RouteLayout.Count][];
        readonly Vector2[] river;
        public Rect Bounds { get; }
        public Rect Town { get; }
        public Rect Road { get; }
        public Vector2 FenceStart { get; }
        public Vector2 FenceEnd { get; }
        public Vector3 Crossing { get; }
        public int RiverPointCount => river.Length;
        public Vector2 RiverPoint(int index) => river[index];

        public RouteTerrain()
        {
            RouteLayout.RiverSpan(out var start, out var end);
            Crossing = (start + end) * 0.5f;
            Town = Rect.MinMaxRect(-73f, 160f, 146f, 242f);
            Bounds = Rect.MinMaxRect(Mathf.Floor((LakeCenter.x - LakeRadius.x - Margin) / 2f) * 2f,
                RouteLayout.Clearing(2).z - RouteLayout.ClearingSize * 0.5f - Margin, 218f, Town.yMax + Margin);
            Road = Rect.MinMaxRect(Bounds.xMin, RouteLayout.Clearing(6).z - RoadWidth * 0.5f,
                150f, RouteLayout.Clearing(6).z + RoadWidth * 0.5f);
            river = new[] { new Vector2(150f, 116f), new Vector2(120f, 121f),
                new Vector2(80f, Crossing.z), new Vector2(20f, Crossing.z),
                new Vector2(-40f, 112f), new Vector2(Bounds.xMin, 108f) };
            FenceStart = river[0];
            FenceEnd = new Vector2(FenceStart.x, Bounds.yMax);
            for (int i = 0; i < paths.Length; i++) paths[i] = RouteLayout.Path(i);
            for (int i = 0; i < openings.Length; i++) openings[i] = RouteLayout.Openings(i);
        }

        public float ClearingRadius(int index, float angle)
        {
            RouteLayout.Clearing(index);
            float noise = Mathf.Sin(angle * 3f + index * 1.7f) * 1.1f + Mathf.Cos(angle * 7f - index) * 0.5f;
            return index == 0 ? 17.35f + noise * 0.09f : 15.9f + noise;
        }

        public bool IsWalkable(float x, float z) => WalkableDistance(x, z) <= 0f;

        public GroundType ForestGround(float x, float z)
        {
            int column = Mathf.FloorToInt((x - Bounds.xMin) / ForestTileSize);
            int row = Mathf.FloorToInt((z - Bounds.yMin) / ForestTileSize);
            return ((column * 17 + row * 31) & 3) == 0 ? GroundType.Branches : GroundType.Leaves;
        }

        public float TrainDistance(float x, float z) => SegmentDistance(new Vector2(x, z),
            Xz(RouteLayout.Clearing(2)), new Vector2(RouteLayout.Clearing(2).x, RouteLayout.TrainNorthEnd));

        public float TrainHeight(float z)
        {
            Vector3 tracks = RouteLayout.Clearing(2);
            float south = tracks.z + RouteLayout.TrainSouthRampLength;
            float north = RouteLayout.TrainNorthEnd - RouteLayout.TrainRampLength;
            float Deck(float at) => Mathf.Max(0f, GroundHeight(tracks.x, at)) + RouteLayout.TrainClearance;
            if (z < south) return Mathf.Lerp(tracks.y, Deck(south), (z - tracks.z) / RouteLayout.TrainSouthRampLength);
            if (z > north) return Mathf.Lerp(Deck(north), GroundHeight(tracks.x, RouteLayout.TrainNorthEnd),
                (z - north) / RouteLayout.TrainRampLength);
            return Deck(z);
        }

        public float WalkableDistance(float x, float z)
        {
            float distance = DistanceToRoute(x, z);
            distance = Mathf.Max(distance, RectangleDistance(new Vector2(x, z), Bounds));
            distance = Mathf.Max(distance, -LakeDistance(x, z));
            float riverBlock = RiverWidth * 0.5f - RiverDistance(x, z);
            riverBlock = Mathf.Min(riverBlock, Mathf.Abs(x - Crossing.x) - CrossingWidth * 0.5f);
            return Mathf.Max(distance, riverBlock);
        }

        public float DistanceToRoute(float x, float z, bool includeRoad = true)
        {
            var point = new Vector2(x, z);
            float distance = includeRoad ? RectangleDistance(point, Road) : float.PositiveInfinity;
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 center = RouteLayout.Clearing(i);
                Vector2 delta = point - Xz(center);
                distance = Mathf.Min(distance, delta.magnitude - ClearingRadius(i, Mathf.Atan2(delta.y, delta.x)));
                foreach (var opening in openings[i])
                    distance = Mathf.Min(distance, SegmentDistance(point, Xz(center), Xz(opening)) - RouteLayout.PathWidth * 0.5f);
            }
            foreach (var path in paths)
                for (int i = 1; i < path.Length; i++)
                    distance = Mathf.Min(distance, SegmentDistance(point, Xz(path[i - 1]), Xz(path[i])) - RouteLayout.PathWidth * 0.5f);
            return distance;
        }

        public float RiverDistance(float x, float z)
        {
            float distance = float.PositiveInfinity;
            for (int i = 1; i < river.Length; i++)
                distance = Mathf.Min(distance, SegmentDistance(new Vector2(x, z), river[i - 1], river[i]));
            return distance;
        }

        public float LakeDistance(float x, float z) =>
            (new Vector2((x - LakeCenter.x) / LakeRadius.x, (z - LakeCenter.y) / LakeRadius.y).magnitude - 1f) * LakeRadius.y;

        public bool IsWater(float x, float z) => LakeDistance(x, z) < 0f || RiverDistance(x, z) < RiverWidth * 0.5f;

        public bool InClearingSquare(float x, float z, float padding = 0f)
        {
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var center = RouteLayout.Clearing(i);
                if (Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(z - center.z)) <= 18f + padding) return true;
            }
            return false;
        }

        public float Height(float x, float z)
        {
            if (IsWalkable(x, z)) return RouteHeight(x, z, out _);
            return GroundHeight(x, z);
        }

        public float GroundHeight(float x, float z)
        {
            float height = RouteHeight(x, z, out float distance);
            float lake = LakeDistance(x, z);
            if (lake < 0f) return -3f * Smooth(-lake / 6f);
            float riverDistance = RiverDistance(x, z);
            if (riverDistance < RiverWidth * 0.5f)
                return Crossing.y - 1.5f * Smooth((RiverWidth * 0.5f - riverDistance) / 0.8f);
            height += 1.5f * Mathf.Sin(x * 0.043f) * Mathf.Cos(z * 0.051f) * Smooth(distance / 18f);
            float cliff = Smooth((x - 75.5f) / 0.5f) * CliffHeight;
            float cliffWeight = Smooth((x - 56f) / 8f) * Smooth((98f - x) / 8f)
                * Smooth((z - 82f) / 8f) * Smooth((108f - z) / 8f) * Smooth((distance - 2f) / 3f);
            return Mathf.Lerp(height, cliff, cliffWeight) * Smooth(lake / 6f) * Smooth((riverDistance - RiverWidth * 0.5f) / 6f);
        }

        float RouteHeight(float x, float z, out float nearest)
        {
            var point = new Vector2(x, z);
            float sum = 0f, weight = 0f;
            nearest = float.PositiveInfinity;
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 center = RouteLayout.Clearing(i);
                float distance = RectangleDistance(point, new Rect(center.x - 18f, center.z - 18f, 36f, 36f));
                if (distance <= 0f) { nearest = 0f; return center.y; }
                Accumulate(distance, center.y, ref nearest, ref sum, ref weight);
            }
            float roadDistance = RectangleDistance(point, Road);
            if (roadDistance <= 0f) { nearest = 0f; return RouteLayout.Clearing(6).y; }
            Accumulate(roadDistance, RouteLayout.Clearing(6).y, ref nearest, ref sum, ref weight);
            for (int from = 0; from < paths.Length; from++)
            {
                for (int i = 1; i < paths[from].Length; i++)
                {
                    Vector3 a = paths[from][i - 1], b = paths[from][i];
                    float distance = SegmentDistance(point, Xz(a), Xz(b)) - RouteLayout.PathWidth * 0.5f - 3f;
                    float height = from == 4 ? RampHeight(point, a, b) : RouteLayout.PathPlaneHeight(from, x, z);
                    if (distance <= 0f) { nearest = 0f; return height; }
                    Accumulate(distance, height, ref nearest, ref sum, ref weight);
                }
            }
            return sum / weight;
        }

        static void Accumulate(float distance, float height, ref float nearest, ref float sum, ref float weights)
        {
            nearest = Mathf.Min(nearest, distance);
            float weight = 1f / Mathf.Pow(Mathf.Max(distance, 0.001f), 4f);
            sum += height * weight;
            weights += weight;
        }

        static float RampHeight(Vector2 point, Vector3 a, Vector3 b)
        {
            if (a.y == b.y) return a.y;
            Vector2 delta = Xz(b - a);
            float length = delta.magnitude;
            float along = Vector2.Dot(point - Xz(a), delta / length);
            return Mathf.Lerp(a.y, b.y, Mathf.Clamp01((along - RampLanding) / (length - 2f * RampLanding)));
        }

        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            float t = delta.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * t);
        }

        public static float RectangleDistance(Vector2 point, Rect rectangle)
        {
            var delta = new Vector2(Mathf.Abs(point.x - rectangle.center.x) - rectangle.width * 0.5f,
                Mathf.Abs(point.y - rectangle.center.y) - rectangle.height * 0.5f);
            return new Vector2(Mathf.Max(delta.x, 0f), Mathf.Max(delta.y, 0f)).magnitude + Mathf.Min(Mathf.Max(delta.x, delta.y), 0f);
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
        static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
    }
}
