using System;

namespace TiffanyGame
{
    public enum EndingStage { Idle, Approach, Fade, Text, Credits }

    public sealed class EndingRules
    {
        public const float ManifestRange = 2.5f;
        public const float ApproachSeconds = 2f;
        public const float FadeSeconds = 1.5f;
        public const float TextSeconds = 4f;

        float elapsed;
        public EndingStage Stage { get; private set; }
        public float ApproachProgress => Math.Min(1f, elapsed / ApproachSeconds);
        public float FadeAlpha => Math.Max(0f, Math.Min(1f, (elapsed - ApproachSeconds) / FadeSeconds));

        public bool TryManifest(bool hasManifest, float distance, bool interactPressed)
        {
            if (Stage != EndingStage.Idle || !hasManifest || !interactPressed
                || !(distance >= 0f && distance < ManifestRange)) return false;
            Stage = EndingStage.Approach;
            return true;
        }

        public void Tick(float dt)
        {
            if (Stage == EndingStage.Idle || !(dt > 0f)) return;
            elapsed = Math.Min(ApproachSeconds + FadeSeconds + TextSeconds, elapsed + dt);
            Stage = elapsed < ApproachSeconds ? EndingStage.Approach
                : elapsed < ApproachSeconds + FadeSeconds ? EndingStage.Fade
                : elapsed < ApproachSeconds + FadeSeconds + TextSeconds ? EndingStage.Text : EndingStage.Credits;
        }
    }
}
