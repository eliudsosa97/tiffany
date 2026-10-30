using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class PosterLayout
    {
        public const int Seed = 271029;
        public const int MaxTotal = 60;
        public const int MaxPerClearing = 12;
        public const float Spacing = 12f;
        public const float DenseSpacing = 5f;
        public const float DenseDistance = 15f;
        public const float MinRouteDistance = 1f;
        public const float MaxRouteDistance = 6f;
        public const float Width = Height * 711f / 1024f;
        public const float Height = 0.6f;

        public enum AnchorKind { Tree, Post, Fence }

        public readonly struct Anchor
        {
            public readonly Vector3 Position;
            public readonly Vector3 Facing;
            public readonly Quaternion Rotation;
            public readonly AnchorKind Kind;

            public Anchor(Vector3 position, Vector3 facing, AnchorKind kind, float yaw = 0f, float roll = 0f)
            {
                Position = position;
                Facing = new Vector3(facing.x, 0f, facing.z).normalized;
                // La cara visible del Quad de Unity apunta a -Z.
                Rotation = Facing == Vector3.zero ? Quaternion.identity
                    : Quaternion.LookRotation(-Facing) * Quaternion.Euler(0f, yaw, roll);
                Kind = kind;
            }
        }

        public static List<Anchor> Select(IReadOnlyList<Anchor> candidates, RouteTerrain terrain)
        {
            var random = new System.Random(Seed);
            var order = new int[candidates.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[other];
                order[other] = swap;
            }
            var chosen = new List<Anchor>(MaxTotal);
            var counts = new int[RouteLayout.Count];
            var visible = new int[RouteLayout.Count];
            foreach (int index in order)
            {
                var anchor = candidates[index];
                Vector3 p = anchor.Position;
                if (anchor.Facing == Vector3.zero || !Eligible(p, terrain)) continue;
                int clearing = ClearingIndex(p);
                if (counts[clearing] >= MaxPerClearing) continue;
                bool free = true;
                for (int i = 0; i < visible.Length; i++)
                    if (InView(p, i) && visible[i] >= MaxPerClearing) free = false;
                foreach (var previous in chosen)
                {
                    float spacing = Mathf.Max(MinimumSpacing(p, terrain), MinimumSpacing(previous.Position, terrain));
                    Vector3 delta = p - previous.Position;
                    if (delta.x * delta.x + delta.z * delta.z < spacing * spacing) free = false;
                }
                if (!free) continue;
                chosen.Add(anchor);
                counts[clearing]++;
                for (int i = 0; i < visible.Length; i++) if (InView(p, i)) visible[i]++;
                if (chosen.Count == MaxTotal) break;
            }
            return chosen;
        }

        public static bool Eligible(Vector3 position, RouteTerrain terrain)
        {
            float distance = terrain.DistanceToRoute(position.x, position.z);
            return distance >= MinRouteDistance && distance <= MaxRouteDistance
                && terrain.Bounds.Contains(new Vector2(position.x, position.z))
                && !terrain.IsWater(position.x, position.z)
                && RouteDirection(position, terrain) != Vector3.zero;
        }

        public static float MinimumSpacing(Vector3 position, RouteTerrain terrain) =>
            RouteTerrain.RectangleDistance(new Vector2(position.x, position.z), terrain.Road) < DenseDistance
            || RouteTerrain.RectangleDistance(new Vector2(position.x, position.z), terrain.Town) < DenseDistance
                ? DenseSpacing : Spacing;

        public static Vector3 RouteDirection(Vector3 position, RouteTerrain terrain)
        {
            const float step = 0.05f;
            return new Vector3(terrain.DistanceToRoute(position.x - step, position.z) - terrain.DistanceToRoute(position.x + step, position.z),
                0f, terrain.DistanceToRoute(position.x, position.z - step) - terrain.DistanceToRoute(position.x, position.z + step)).normalized;
        }

        public static int ClearingIndex(Vector3 position)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 delta = position - RouteLayout.Clearing(i);
                float squared = delta.x * delta.x + delta.z * delta.z;
                if (squared >= distance) continue;
                distance = squared;
                nearest = i;
            }
            return nearest;
        }

        public static bool InView(Vector3 position, int clearing)
        {
            Vector3 delta = position - RouteLayout.Clearing(clearing);
            return Mathf.Abs(delta.x) <= 40f && Mathf.Abs(delta.z) <= 40f;
        }
    }
}
