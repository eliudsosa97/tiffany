using UnityEngine;

namespace TiffanyGame
{
    public struct PlayerLean
    {
        public const float MaxAngle = 10f;
        public const float FullTurnSpeed = 360f;
        public const float SmoothTime = 0.12f;
        public float Roll { get; private set; }
        float velocity;

        public float Advance(float speed, float angularSpeed, float dt)
        {
            if (dt <= 0f) return Roll;
            float running = Mathf.InverseLerp(PlayerMotion.WalkSpeed, PlayerMotion.RunSpeed, speed);
            float target = -MaxAngle * Mathf.Clamp(angularSpeed / FullTurnSpeed * speed / PlayerMotion.RunSpeed * running, -1f, 1f);
            Roll = Mathf.SmoothDamp(Roll, target, ref velocity, SmoothTime, Mathf.Infinity, dt);
            return Roll;
        }
    }
}
