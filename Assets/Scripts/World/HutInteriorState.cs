using UnityEngine;

namespace TiffanyGame
{
    public sealed class HutInteriorState
    {
        public bool IsInside { get; private set; }

        public bool Update(Vector3 localPosition, Bounds interior, float exitMargin)
        {
            if (IsInside) interior.Expand(2f * Mathf.Max(0f, exitMargin));
            IsInside = interior.Contains(localPosition);
            return IsInside;
        }

        public void Reset() => IsInside = false;
    }
}
