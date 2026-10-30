using UnityEngine;

namespace TiffanyGame
{
    public sealed class FlashChargeIndicator
    {
        public const float FadeSeconds = 0.25f;
        public static readonly Color Warm = new Color(1f, 0.92f, 0.78f);
        public static readonly Color LowEnergy = new Color(0.55f, 0.18f, 0.16f);
        float remaining;
        bool wasComplete;

        public bool Visible { get; private set; }
        public float Fill { get; private set; }
        public float Scale => remaining > 0f ? 1.3f : 1f;
        public Color Tint { get; private set; }

        public void Tick(float dt, FlashCharge charge, float energy, InterfaceScreen screen, bool blocked)
        {
            bool complete = charge.Progress >= 1f;
            bool allowed = screen == InterfaceScreen.Playing && !blocked;
            if (!allowed) remaining = 0f;
            else if (complete && !wasComplete) remaining = FadeSeconds;
            else remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, dt));
            wasComplete = complete;
            Visible = allowed && (charge.Attempting || remaining > 0f);
            Fill = remaining > 0f ? 1f : charge.Charging ? charge.Progress : charge.AttemptProgress;
            var tint = remaining > 0f || energy >= SoulCosts.Flash ? Warm : LowEnergy;
            tint.a = remaining > 0f ? remaining / FadeSeconds : 1f;
            Tint = tint;
        }
    }
}
