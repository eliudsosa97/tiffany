using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class PlayerMotion
    {
        public const float SlowSpeed = 1.1f;
        public const float WalkSpeed = 2.2f;
        public const float RunSpeed = 4f;
        public const float SlowWalkThreshold = (SlowSpeed + WalkSpeed) / 2f;
        public const float WalkRunThreshold = (WalkSpeed + RunSpeed) / 2f;

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

        public static Gait GaitForSpeed(float speed)
        {
            if (speed < SlowWalkThreshold) return Gait.Slow;
            return speed < WalkRunThreshold ? Gait.Walk : Gait.Run;
        }

        public static Gait NoiseGait(Gait last, float targetSpeed, float currentSpeed)
        {
            if (targetSpeed <= 0f) return last;
            var next = GaitForSpeed(targetSpeed);
            return next >= last || currentSpeed <= targetSpeed ? next : last;
        }

        public static Vector3 CameraRelative(Vector2 input, float cameraYawDegrees)
        {
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
        }
    }
}
