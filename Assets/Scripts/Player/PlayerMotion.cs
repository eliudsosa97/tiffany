using System;
using UnityEngine;

namespace ElVelador
{
    public static class PlayerMotion
    {
        public const float SlowSpeed = 1.1f;
        public const float WalkSpeed = 2.2f;
        public const float RunSpeed = 4f;

        public static Gait ResolveGait(bool slow, bool run)
        {
            return run ? Gait.Run : slow ? Gait.Slow : Gait.Walk;
        }

        public static float Speed(Gait gait)
        {
            return gait switch
            {
                Gait.Slow => SlowSpeed,
                Gait.Walk => WalkSpeed,
                Gait.Run => RunSpeed,
                _ => throw new ArgumentOutOfRangeException(nameof(gait))
            };
        }

        public static Vector3 CameraRelative(Vector2 input, float cameraYawDegrees)
        {
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
        }
    }
}
