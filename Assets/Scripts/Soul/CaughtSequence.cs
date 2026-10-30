using UnityEngine;

namespace TiffanyGame
{
    public enum CaughtPhase { Idle, Fire, FadeOut, FadeIn }

    public sealed class CaughtSequence
    {
        public const float FireDuration = 0.35f;
        public const float FadeOutDuration = 0.8f;
        public const float FadeInDuration = 0.9f;

        public CaughtPhase Phase { get; private set; }
        public bool Active => Phase != CaughtPhase.Idle;
        public bool RespawnDue { get; private set; }
        public float Overlay => Phase == CaughtPhase.Fire ? Mathf.Clamp01(elapsed * 4f / FireDuration)
            : Phase == CaughtPhase.FadeOut ? 1f
            : Phase == CaughtPhase.FadeIn
                ? Mathf.Clamp01(1f - (elapsed - (FireDuration + FadeOutDuration)) / FadeInDuration) : 0f;
        public float FireAmount => Phase == CaughtPhase.Fire || Phase == CaughtPhase.FadeOut
            ? Mathf.Clamp01(1f - elapsed / (FireDuration + FadeOutDuration)) : 0f;

        private float elapsed;

        public void Cancel()
        {
            elapsed = 0f;
            Phase = CaughtPhase.Idle;
            RespawnDue = false;
        }

        public void Begin()
        {
            if (Active) return;
            elapsed = 0f;
            RespawnDue = false;
            Phase = CaughtPhase.Fire;
        }

        public void Tick(float dt)
        {
            RespawnDue = false;
            if (!Active || dt <= 0f || float.IsNaN(dt)) return;
            elapsed += dt;
            RespawnDue = (Phase == CaughtPhase.Fire || Phase == CaughtPhase.FadeOut)
                && elapsed >= FireDuration + FadeOutDuration;
            Phase = elapsed < FireDuration ? CaughtPhase.Fire
                : elapsed < FireDuration + FadeOutDuration ? CaughtPhase.FadeOut
                : elapsed < FireDuration + FadeOutDuration + FadeInDuration ? CaughtPhase.FadeIn : CaughtPhase.Idle;
        }
    }
}
