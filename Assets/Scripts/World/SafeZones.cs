using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public static class SafeZones
    {
        private struct Zone
        {
            public int handle;
            public Vector3 center;
            public float radius;
        }

        private static readonly List<Zone> zones = new List<Zone>();
        private static int nextHandle = 1;

        public static int Count => zones.Count;

        public static int Add(Vector3 center, float radius)
        {
            if (radius <= 0f || float.IsNaN(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            int handle = nextHandle++;
            zones.Add(new Zone { handle = handle, center = center, radius = radius });
            return handle;
        }

        public static void Remove(int handle)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].handle == handle)
                {
                    zones.RemoveAt(i);
                    return;
                }
            }
        }

        public static bool Contains(Vector3 point)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                Vector3 offset = point - zones[i].center;
                offset.y = 0f;
                if (offset.magnitude < zones[i].radius)
                {
                    return true;
                }
            }
            return false;
        }

        public static Vector3 PushOut(Vector3 point, float margin)
        {
            TryPushOut(point, margin, out Vector3 result);
            return result;
        }

        public static bool TryPushOut(Vector3 point, float margin, out Vector3 result)
        {
            margin = Mathf.Max(0f, margin);
            result = point;
            if (IsOutsideExpandedZones(point, margin))
            {
                return true;
            }

            margin += 1e-3f;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones[i];
                float radius = zone.radius + margin;
                Vector3 offset = point - zone.center;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < radius)
                {
                    Vector3 direction = distance > 0f ? offset / distance : Vector3.forward;
                    Vector3 candidate = zone.center + direction * radius;
                    candidate.y = point.y;
                    ConsiderCandidate(point, candidate, margin, ref result, ref bestDistanceSquared);
                }

                for (int j = i + 1; j < zones.Count; j++)
                {
                    Zone other = zones[j];
                    float otherRadius = other.radius + margin;
                    Vector3 separation = other.center - zone.center;
                    separation.y = 0f;
                    float centerDistance = separation.magnitude;
                    if (centerDistance == 0f || centerDistance > radius + otherRadius ||
                        centerDistance < Mathf.Abs(radius - otherRadius))
                    {
                        continue;
                    }

                    float along = (radius * radius - otherRadius * otherRadius +
                        centerDistance * centerDistance) / (2f * centerDistance);
                    float height = Mathf.Sqrt(Mathf.Max(0f, radius * radius - along * along));
                    Vector3 axis = separation / centerDistance;
                    Vector3 midpoint = zone.center + axis * along;
                    midpoint.y = point.y;
                    Vector3 perpendicular = new Vector3(-axis.z, 0f, axis.x) * height;
                    ConsiderCandidate(point, midpoint + perpendicular, margin, ref result, ref bestDistanceSquared);
                    ConsiderCandidate(point, midpoint - perpendicular, margin, ref result, ref bestDistanceSquared);
                }
            }

            return bestDistanceSquared < float.PositiveInfinity;
        }

        private static void ConsiderCandidate(Vector3 point, Vector3 candidate, float margin,
            ref Vector3 result, ref float bestDistanceSquared)
        {
            float distanceSquared = (candidate - point).sqrMagnitude;
            if (distanceSquared < bestDistanceSquared && IsOutsideExpandedZones(candidate, margin - 1e-4f))
            {
                bestDistanceSquared = distanceSquared;
                result = candidate;
            }
        }

        private static bool IsOutsideExpandedZones(Vector3 point, float margin)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                Vector3 offset = point - zones[i].center;
                offset.y = 0f;
                float radius = zones[i].radius + margin;
                if (offset.sqrMagnitude < radius * radius)
                {
                    return false;
                }
            }

            return true;
        }

        public static void Clear()
        {
            zones.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Clear();
            nextHandle = 1;
        }
    }
}
