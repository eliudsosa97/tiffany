using System;

namespace TiffanyGame
{
    public sealed class MemoryEcho
    {
        public const float Duration = 10f;
        public const float Fade = 1f;

        float elapsed = Duration;
        float duration = Duration;

        public bool Active => elapsed < duration;
        public float Elapsed => elapsed;
        public float Weight => Active ? Math.Min(1f, Math.Min(elapsed, duration - elapsed) / Fade) : 0f;

        public void Start(float seconds = Duration)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            duration = seconds;
            elapsed = 0f;
        }

        public void Stop() => elapsed = duration;

        public void Tick(float dt)
        {
            if (Active && dt > 0f) elapsed = Math.Min(duration, elapsed + dt);
        }
    }
}
