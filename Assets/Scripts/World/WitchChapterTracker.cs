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
        public Bounds[] ClearingBounds => bounds;

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
            int clearing = RouteChapters.FindClearing(player.position, bounds);
            if (clearing >= 0) witch.SetChapter(RouteChapters.Resolve(witch.chapter, clearing + 1));
        }
    }
}
