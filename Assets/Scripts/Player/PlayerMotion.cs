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
        public const float SmoothTime = 0.07f;
        public const float StopSpeed = 0.05f;

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

        public static Gait NoiseGait(float currentSpeed) => GaitForSpeed(currentSpeed);

        public static float TargetSpeed(float inputMagnitude, Gait gait)
        {
            float amount = Mathf.Clamp01(inputMagnitude);
            if (gait == Gait.Slow) return SlowSpeed * amount;
            float walk = WalkSpeed * Mathf.Clamp01(amount * 2f);
            return gait == Gait.Run ? walk + (RunSpeed - WalkSpeed) * Mathf.Clamp01(amount * 2f - 1f) : walk;
        }

        public struct VelocityState
        {
            public Vector3 Velocity { get; private set; }
            Vector3 acceleration;

            public Vector3 Advance(Vector3 target, float dt)
            {
                if (dt <= 0f) return Velocity;
                target.y = 0f;
                Velocity = Vector3.SmoothDamp(Velocity, target, ref acceleration, SmoothTime, Mathf.Infinity, dt);
                if (target.sqrMagnitude == 0f && Velocity.sqrMagnitude <= StopSpeed * StopSpeed)
                {
                    Velocity = Vector3.zero;
                    acceleration = Vector3.zero;
                }
                return Velocity;
            }
        }

        public static Vector3 CameraRelative(Vector2 input, float cameraYawDegrees)
        {
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * new Vector3(input.x, 0f, input.y);
        }
    }
}
