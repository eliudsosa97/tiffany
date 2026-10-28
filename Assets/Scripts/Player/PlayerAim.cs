using UnityEngine;

namespace ElVelador
{
    public enum AimSource { Move, Stick, Pointer }

    public static class PlayerAim
    {
        public const float PointerDeadzone = 0.2f;

        public static AimSource NextSource(AimSource current, Vector2 aimStick, bool pointerMoved, bool pointerAllowed)
        {
            if (aimStick.sqrMagnitude > 0f) return AimSource.Stick;
            if (pointerAllowed && (pointerMoved || current == AimSource.Pointer)) return AimSource.Pointer;
            return AimSource.Move;
        }

        public static bool TryDirection(AimSource source, Vector3 moveDirection, Vector3 stickDirection,
            Vector3 pointerOffset, out Vector3 direction)
        {
            direction = source switch
            {
                AimSource.Stick => stickDirection,
                AimSource.Pointer => pointerOffset,
                _ => moveDirection
            };
            direction.y = 0f;
            return source == AimSource.Pointer
                ? direction.sqrMagnitude > PointerDeadzone * PointerDeadzone
                : direction.sqrMagnitude > 0f;
        }
    }
}
