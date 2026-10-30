using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class HutRoofCutaway : MonoBehaviour
    {
        public Transform player;
        public BoxCollider interior;
        public Renderer[] roofRenderers = Array.Empty<Renderer>();
        public float exitMargin = 0.25f;

        readonly HutInteriorState state = new HutInteriorState();

        void Update() => Refresh();

        public void Refresh()
        {
            if (player == null || interior == null) return;
            bool wasInside = state.IsInside;
            bool inside = state.Update(interior.transform.InverseTransformPoint(player.position),
                new Bounds(interior.center, interior.size), exitMargin);
            if (inside != wasInside) SetRoofVisible(!inside);
        }

        void OnDisable()
        {
            state.Reset();
            SetRoofVisible(true);
        }

        void SetRoofVisible(bool visible)
        {
            foreach (var renderer in roofRenderers)
                if (renderer != null) renderer.enabled = visible;
        }
    }
}
