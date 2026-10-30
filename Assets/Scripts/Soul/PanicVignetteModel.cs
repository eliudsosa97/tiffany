using UnityEngine;

namespace TiffanyGame
{
    public struct PanicVignetteModel
    {
        public const float SmoothTime = 0.25f;
        float intensity;

        public float Alpha => 0.85f * intensity;
        public float Scale => Mathf.Lerp(1.6f, 1f, intensity);

        public static float Intensity(float panic) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(panic));

        public void Advance(float panic, float dt)
        {
            if (dt <= 0f) return;
            intensity = Mathf.Lerp(intensity, Intensity(panic), 1f - Mathf.Exp(-dt / SmoothTime));
        }
    }
}
