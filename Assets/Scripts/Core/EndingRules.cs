using System;

namespace TiffanyGame
{
    public enum EndingStage { Idle, Fade, Text, Credits }

    public sealed class EndingRules
    {
        public const float ManifestRange = 2.5f;
        public const float FadeSeconds = 1.5f;
        public const float TextSeconds = 4f;

        float elapsed;
        public EndingStage Stage { get; private set; }
        public float FadeAlpha => Math.Min(1f, elapsed / FadeSeconds);

        public bool TryManifest(bool hasManifest, float distance, bool interactPressed)
        {
            if (Stage != EndingStage.Idle || !hasManifest || !interactPressed
                || !(distance >= 0f && distance < ManifestRange)) return false;
            Stage = EndingStage.Fade;
            return true;
        }

        public void Tick(float dt)
        {
            if (Stage == EndingStage.Idle || !(dt > 0f)) return;
            elapsed = Math.Min(FadeSeconds + TextSeconds, elapsed + dt);
            Stage = elapsed < FadeSeconds ? EndingStage.Fade
                : elapsed < FadeSeconds + TextSeconds ? EndingStage.Text : EndingStage.Credits;
        }
    }
}
