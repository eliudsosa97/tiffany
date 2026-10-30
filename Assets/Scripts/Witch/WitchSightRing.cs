using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSightRing
    {
        public float Radius { get; private set; }
        public float Alpha { get; private set; }
        public bool Visible { get; private set; }
        private readonly WitchSenseProfile profile;
        private float target, start, progress = 1f, fade, phase;

        public WitchSightRing(WitchSenseProfile profile, bool lightOn)
        {
            this.profile = profile;
            Radius = target = start = lightOn ? profile.LitSightDistance : profile.DarkSightDistance;
        }

        public void Tick(float dt, bool active, WitchForm form, bool lightOn, float distance)
        {
            float next = lightOn ? profile.LitSightDistance : profile.DarkSightDistance;
            if (next != target) { start = Radius; target = next; progress = 0f; }
            progress = Mathf.Min(1f, progress + dt / 0.4f);
            Radius = Mathf.SmoothStep(start, target, progress);
            Visible = active && form == WitchForm.Grounded;
            fade = Mathf.MoveTowards(fade, Visible ? 1f : 0f, dt / 0.25f);
            phase = Mathf.Repeat(phase + dt * Mathf.PI * 3f, Mathf.PI * 2f);
            Alpha = fade * (distance <= target ? 0.45f + 0.025f * Mathf.Sin(phase) : 0.18f);
        }
    }
}
