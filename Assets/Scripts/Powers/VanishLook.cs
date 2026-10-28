using UnityEngine;

namespace TiffanyGame
{
    public static class VanishLook
    {
        public const float FadeTime = 0.35f;

        public static float Visibility(float remaining, float duration)
        {
            if (remaining <= 0f) return 1f;
            float elapsed = duration - remaining;
            if (elapsed < FadeTime) return Mathf.Clamp01(1f - elapsed / FadeTime);
            return Mathf.Clamp01(1f - remaining / FadeTime);
        }

        public static bool FigureShown(float visibility, float time)
        {
            if (visibility >= 1f) return true;
            if (visibility <= 0f) return false;
            return Mathf.PerlinNoise(time * 30f, 0.5f) < visibility;
        }
    }
}
