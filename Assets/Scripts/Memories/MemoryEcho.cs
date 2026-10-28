using System;

namespace TiffanyGame
{
    public sealed class MemoryEcho
    {
        public const float Duration = 10f;
        public const float Fade = 1f;

        float elapsed = Duration;

        public bool Active => elapsed < Duration;
        public float Weight => Active ? Math.Min(1f, Math.Min(elapsed, Duration - elapsed) / Fade) : 0f;

        public void Start() => elapsed = 0f;

        public void Tick(float dt)
        {
            if (Active && dt > 0f) elapsed = Math.Min(Duration, elapsed + dt);
        }
    }
}
