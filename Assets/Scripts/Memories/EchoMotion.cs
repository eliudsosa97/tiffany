using System;

namespace TiffanyGame
{
    public static class EchoMotion
    {
        const float SmoothingTime = 0.1f;

        public static float Speed(float displacement, float dt, float previousSpeed)
        {
            if (dt <= 0f) return previousSpeed;
            float blend = (float)(1d - Math.Exp(-dt / SmoothingTime));
            return previousSpeed + (displacement / dt - previousSpeed) * blend;
        }
    }
}
