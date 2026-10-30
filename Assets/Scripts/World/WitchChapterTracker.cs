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
        public int LastVisitedClearingIndex { get; private set; } = -1;
        public bool HasNextClearing => bounds != null && LastVisitedClearingIndex + 1 < bounds.Length
            && bounds[LastVisitedClearingIndex + 1].size != Vector3.zero;
        public Vector3 NextClearingPosition => HasNextClearing ? bounds[LastVisitedClearingIndex + 1].center : Vector3.zero;

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
            if (player == null) return;
            int clearing = RouteChapters.FindClearing(player.position, bounds);
            if (!RouteChapters.CanVisitClearing(LastVisitedClearingIndex, clearing)) return;
            LastVisitedClearingIndex = clearing;
            if (witch != null && witch.chapter != Chapter.Return)
                witch.SetChapter(RouteChapters.Resolve(witch.chapter, clearing + 1));
        }
    }
}
