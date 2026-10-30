using UnityEngine;

namespace TiffanyGame
{
    public struct LegFacing
    {
        public const float BackEnter = 120f;
        public const float BackExit = 100f;
        public const float MaxTwist = 85f;
        public const float LegsSmoothTime = 0.08f;
        public const float TwistSmoothTime = 0.07f;
        public const float MaxLegsSpeed = 720f;
        public float LegsOffset { get; private set; }
        public float Twist { get; private set; }
        public bool Backward { get; private set; }
        float legsVelocity, twistVelocity;

        public void Advance(float bodyYaw, Vector3 velocity, bool running, float dt)
        {
            if (dt <= 0f) return;
            velocity.y = 0f;
            bool moving = velocity.sqrMagnitude > TiffanyAnimation.MoveSpeed * TiffanyAnimation.MoveSpeed;
            float offset = Mathf.DeltaAngle(bodyYaw, Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg);
            Backward = moving && !running && (Backward ? Mathf.Abs(offset) >= BackExit : Mathf.Abs(offset) > BackEnter);
            float target = !moving || running ? 0f : Backward ? Mathf.DeltaAngle(0f, offset + 180f) : offset;
            LegsOffset = Mathf.SmoothDamp(LegsOffset, target, ref legsVelocity, LegsSmoothTime, MaxLegsSpeed, dt);
            Twist = Mathf.SmoothDamp(Twist, Mathf.Clamp(-LegsOffset, -MaxTwist, MaxTwist),
                ref twistVelocity, TwistSmoothTime, Mathf.Infinity, dt);
        }
    }
}
