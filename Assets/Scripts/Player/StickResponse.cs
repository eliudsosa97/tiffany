using UnityEngine;

namespace TiffanyGame
{
    public static class StickResponse
    {
        public const float Deadzone = 0.15f;
        public const float WalkPoint = 0.5f;

        public static Vector2 Shape(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= Deadzone) return Vector2.zero;
            float amount = magnitude <= WalkPoint
                ? WalkPoint * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Deadzone, WalkPoint, magnitude))
                : Mathf.Lerp(WalkPoint, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WalkPoint, 1f, magnitude)));
            return raw / magnitude * amount;
        }
    }
}
