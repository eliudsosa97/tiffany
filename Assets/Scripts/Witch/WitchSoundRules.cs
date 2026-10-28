using UnityEngine;
namespace TiffanyGame
{
    public sealed class WitchSoundRules
    {
        public const float FireNear = 6f, FireFar = 40f, BreathNear = 3f, BreathFar = 15f;
        public const float OneShotNear = 5f, OneShotFar = 30f, CackleCooldown = 40f;
        public const float StepInterval = 0.72f, StepReferenceSpeed = 1.2f;
        private static readonly float[] StepFactors = { 0.85f, 1.2f, 0.95f, 1.1f };
        private static readonly int[] StepClips = { 0, 2, 1, 3 };
        private WitchState? previous;
        private float stepTime, sinceCackle = CackleCooldown;
        private int stepIndex, cackleIndex, cackleEntries;
        public float FireVolume { get; private set; }
        public float BreathVolume { get; private set; }
        public float OneShotVolume { get; private set; }
        public bool Landed { get; private set; }
        public bool Step { get; private set; }
        public bool Crack { get; private set; }
        public bool Whoosh { get; private set; }
        public bool Cackle { get; private set; }
        public int StepClip { get; private set; }
        public int CackleClip { get; private set; }
        public static float Volume(float distance, float near, float far) => Mathf.Clamp01((far - distance) / (far - near));
        public void Tick(float dt, in WitchDecision decision, float distanceToPlayer)
        {
            Landed = Step = Crack = Whoosh = Cackle = false;
            bool grounded = decision.Form == WitchForm.Grounded;
            WitchState state = decision.State;
            FireVolume = decision.Form == WitchForm.Flying && decision.FireLit ? Volume(distanceToPlayer, FireNear, FireFar) : 0f;
            BreathVolume = grounded && (state == WitchState.Stalking || state == WitchState.WindUp || state == WitchState.Recovering)
                ? Volume(distanceToPlayer, BreathNear, BreathFar) : 0f;
            OneShotVolume = Volume(distanceToPlayer, OneShotNear, OneShotFar);
            if (dt <= 0f || float.IsNaN(dt)) return;
            sinceCackle += dt;
            if (previous.HasValue && state != previous.Value)
            {
                Landed = previous == WitchState.Landing && grounded;
                Crack = state == WitchState.WindUp;
                Whoosh = state == WitchState.Sprinting;
                bool occasional = state == WitchState.Recovering || state == WitchState.TakingOff;
                Cackle = state == WitchState.Caught || (occasional && ++cackleEntries % 2 == 0 && sinceCackle >= CackleCooldown);
                if (Cackle)
                {
                    CackleClip = cackleIndex;
                    cackleIndex = (cackleIndex + 1) % 3;
                    sinceCackle = 0f;
                }
            }
            previous = state;
            bool walking = state == WitchState.Stalking && grounded && decision.Speed > 0f;
            stepTime = walking ? stepTime + dt : 0f;
            if (!walking) return;
            float interval = StepInterval * StepReferenceSpeed / decision.Speed * StepFactors[stepIndex];
            if (stepTime < interval) return;
            Step = true;
            stepTime -= interval;
            StepClip = StepClips[stepIndex];
            stepIndex = (stepIndex + 1) % StepFactors.Length;
        }
    }
}
