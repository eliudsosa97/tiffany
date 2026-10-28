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
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                for (int i = 0; i < zones.Count; i++)
                {
                    Zone zone = zones[i];
                    Vector3 offset = point - zone.center;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    float boundary = zone.radius + margin;
                    if (distance < boundary)
                    {
                        Vector3 direction = distance > 0f ? offset / distance : Vector3.forward;
                        point.x = zone.center.x + direction.x * boundary;
                        point.z = zone.center.z + direction.z * boundary;
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
            }
            return point;
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
