using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class TrailBirdLayout
    {
        public readonly struct Point
        {
            public readonly Vector3 Position;
            public readonly float Distance;
            public readonly int BendIndex;
            public readonly int Side;

            public Point(Vector3 position, float distance, int bendIndex, int side)
            {
                Position = position;
                Distance = distance;
                BendIndex = bendIndex;
                Side = side;
            }
        }

        public static List<Point> Points(Vector3[] path, RouteTerrain terrain, int seed)
        {
            if (path == null || path.Length < 2) throw new ArgumentException("El sendero necesita dos puntos.", nameof(path));
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            var distances = new float[path.Length];
            var anchors = new List<int> { 0 };
            for (int i = 1; i < path.Length; i++)
            {
                float length = Vector3.Distance(path[i - 1], path[i]);
                if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length))
                    throw new ArgumentException("El sendero contiene un segmento inválido.", nameof(path));
                distances[i] = distances[i - 1] + length;
                if (i < path.Length - 1 && Vector2.Angle(Xz(path[i] - path[i - 1]), Xz(path[i + 1] - path[i])) > 20f)
                    anchors.Add(i);
            }
            anchors.Add(path.Length - 1);
            var random = new System.Random(seed);
            var points = new List<Point>();
            int side = 1;
            for (float at = 0f; at <= distances[path.Length - 1]; at += 15f + (float)random.NextDouble() * 5f)
            {
                if (!NearBend(at)) Add(at, -1);
            }
            float end = distances[path.Length - 1];
            if (!NearBend(end) && (points.Count == 0 || end - points[points.Count - 1].Distance >= 15f)) Add(end, -1);
            for (int anchor = 1; anchor < anchors.Count - 1; anchor++) Add(distances[anchors[anchor]], anchors[anchor]);
            points.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 1; i < points.Count;)
            {
                var a = points[i - 1];
                var b = points[i];
                float gap = Vector3.Distance(a.Position, b.Position);
                if (gap <= 25f) { i++; continue; }
                int insertSide = 1;
                for (int before = 0; before < i; before++)
                    if (points[before].BendIndex < 0) insertSide = -points[before].Side;
                float best = gap;
                Point split = default;
                for (float at = a.Distance + 0.5f; at < b.Distance - 0.5f; at += 0.5f)
                {
                    if (!TryPoint(path, distances, at, -1, insertSide, 3.25f, terrain, out var candidate)) continue;
                    float score = Mathf.Max(Vector3.Distance(a.Position, candidate.Position), Vector3.Distance(candidate.Position, b.Position));
                    if (score >= best - 0.01f) continue;
                    best = score;
                    split = candidate;
                }
                if (best == gap) throw new InvalidOperationException("No hay una percha seca que mantenga la cadena a 25 m.");
                points.Insert(i, split);
                int nextSide = -split.Side;
                for (int after = i + 1; after < points.Count; after++)
                {
                    var point = points[after];
                    if (point.BendIndex >= 0) continue;
                    if (point.Side != nextSide)
                    {
                        if (!TryPoint(path, distances, point.Distance, -1, nextSide, 3.25f, terrain, out point))
                        {
                            points.RemoveAt(after--);
                            continue;
                        }
                        points[after] = point;
                    }
                    nextSide = -nextSide;
                }
            }
            return points;

            bool NearBend(float at)
            {
                for (int anchor = 1; anchor < anchors.Count - 1; anchor++)
                    if (Mathf.Abs(at - distances[anchors[anchor]]) < 2f) return true;
                return false;
            }

            void Add(float at, int bend)
            {
                if (TryPoint(path, distances, at, bend, side, 3.1f + (float)random.NextDouble() * 0.3f, terrain, out var point))
                {
                    points.Add(point);
                    if (bend < 0) side = -side;
                }
                else if (bend >= 0) throw new InvalidOperationException($"El desvío {bend} no tiene una percha seca fuera del sendero.");
            }
        }

        static bool TryPoint(Vector3[] path, float[] distances, float at, int bend, int side, float offset,
            RouteTerrain terrain, out Point point)
        {
            int segment = 0;
            while (segment < path.Length - 2 && at > distances[segment + 1]) segment++;
            Vector3 center = Vector3.Lerp(path[segment], path[segment + 1],
                (at - distances[segment]) / (distances[segment + 1] - distances[segment]));
            Vector2 direction = Xz(path[segment + 1] - path[segment]).normalized;
            Vector2 normal = new Vector2(direction.y, -direction.x);
            if (bend >= 0)
            {
                Vector2 incoming = Xz(path[bend] - path[bend - 1]).normalized;
                Vector2 outgoing = Xz(path[bend + 1] - path[bend]).normalized;
                Vector2 average = (incoming + outgoing).normalized;
                if (average.sqrMagnitude > 0f) normal = new Vector2(average.y, -average.x);
                side = incoming.x * outgoing.y - incoming.y * outgoing.x >= 0f ? 1 : -1;
            }
            Vector3 position = center + new Vector3(normal.x, 0f, normal.y) * (offset * side);
            float axisDistance = float.PositiveInfinity;
            for (int i = 1; i < path.Length; i++)
                axisDistance = Mathf.Min(axisDistance, RouteTerrain.SegmentDistance(Xz(position), Xz(path[i - 1]), Xz(path[i])));
            if (axisDistance >= 3f && axisDistance <= 3.5f && !terrain.IsWater(position.x, position.z) && !InRiverSpan(position))
            {
                position.y = terrain.Height(position.x, position.z);
                point = new Point(position, at, bend, side);
                return true;
            }
            point = default;
            return false;
        }

        static bool InRiverSpan(Vector3 position)
        {
            RouteLayout.RiverSpan(out var start, out var end);
            Vector2 delta = Xz(end - start), offset = Xz(position - start);
            float along = Vector2.Dot(offset, delta) / delta.sqrMagnitude;
            return along >= 0f && along <= 1f && RouteTerrain.SegmentDistance(Xz(position), Xz(start), Xz(end)) <= RouteLayout.PathWidth * 0.5f + 3.5f;
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
    }
}
