using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchChapterTracker : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public Transform[] clearings = Array.Empty<Transform>();

        private Bounds[] bounds;

        private void Start()
        {
            bounds = new Bounds[clearings.Length];
            for (int i = 0; i < clearings.Length; i++)
            {
                var clearing = clearings[i];
                if (clearing == null) continue;
                bool found = false;
                for (int j = 0; j < clearing.childCount; j++)
                {
                    var child = clearing.GetChild(j);
                    if (!child.TryGetComponent<GroundSurface>(out _) || !child.TryGetComponent<Collider>(out var floor)) continue;
                    if (found) bounds[i].Encapsulate(floor.bounds);
                    else bounds[i] = floor.bounds;
                    found = true;
                }
            }
        }

        private void Update()
        {
            if (witch == null || player == null || witch.chapter == Chapter.Return) return;
            Vector3 position = player.position;
            for (int i = 0; i < bounds.Length; i++)
            {
                if (bounds[i].size == Vector3.zero) continue;
                Vector3 min = bounds[i].min;
                Vector3 max = bounds[i].max;
                if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z) continue;
                witch.SetChapter(RouteChapters.Resolve(witch.chapter, i + 1));
                return;
            }
        }
    }
}
