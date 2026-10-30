using System;
namespace TiffanyGame
{
    public sealed class WitchPresenceRules
    {
        public const float NearDistance = 20f, FadeIn = 0.6f, FadeOut = 1f;
        public float Volume { get; private set; }
        public bool Audible => Volume > 0f;
        public void Tick(float dt, WitchForm form, WitchState state, float distance, bool onScreen)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            bool audible = form == WitchForm.Grounded && state == WitchState.Stalking && distance < NearDistance && !onScreen;
            Volume = audible ? Math.Min(1f, Volume + dt / FadeIn) : Math.Max(0f, Volume - dt / FadeOut);
        }
    }
}
