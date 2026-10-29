using UnityEngine;

namespace TiffanyGame
{
    public enum WitchJerkPhase { Off, Still, Tremble, Lunge }

    public sealed class WitchJerk
    {
        public const float StillMin = 0.5f, StillMax = 1.1f, TrembleMin = 0.2f, TrembleMax = 0.35f;
        public const float LungeMin = 0.45f, LungeMax = 0.6f;
        public const float IdleGapMin = 2f, IdleGapMax = 5f, IdleTrembleMin = 0.15f, IdleTrembleMax = 0.3f;
        public const float StillAnimator = 0.04f, TrembleAnimator = 0.15f, LungeAnimator = 1.8f;
        private readonly System.Random random;
        private bool active, moving;
        private float elapsed, stillDuration, trembleDuration, lungeFactor;

        public WitchJerkPhase Phase { get; private set; }
        public float Duration { get; private set; }
        public bool PhaseStarted { get; private set; }
        public float SpeedFactor { get; private set; } = 1f;
        public float AnimatorSpeed { get; private set; } = 1f;
        public float Tremble { get; private set; }

        public WitchJerk(int seed) => random = new System.Random(seed);

        public void Tick(float dt, in WitchDecision decision)
        {
            if (dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0f;
            bool on = decision.Form == WitchForm.Grounded
                && (decision.State == WitchState.Stalking || decision.State == WitchState.Suspicious);
            bool walking = on && decision.State == WitchState.Stalking && decision.Speed > 0f;
            PhaseStarted = false;
            if (!on) (active, Phase) = (false, WitchJerkPhase.Off);
            else if (!active || walking != moving)
            {
                active = true;
                moving = walking;
                elapsed = 0f;
                Begin(walking ? WitchJerkPhase.Lunge : WitchJerkPhase.Off);
            }
            else
            {
                elapsed += dt;
                while (elapsed >= Duration)
                {
                    elapsed -= Duration;
                    Begin(Next());
                }
            }
            Output();
        }

        private WitchJerkPhase Next() => Phase switch
        {
            WitchJerkPhase.Lunge => WitchJerkPhase.Still,
            WitchJerkPhase.Still => WitchJerkPhase.Tremble,
            WitchJerkPhase.Tremble => moving ? WitchJerkPhase.Lunge : WitchJerkPhase.Off,
            _ => WitchJerkPhase.Tremble
        };

        private void Begin(WitchJerkPhase phase)
        {
            Phase = phase;
            PhaseStarted = true;
            switch (phase)
            {
                case WitchJerkPhase.Lunge:
                    Duration = Range(LungeMin, LungeMax);
                    stillDuration = Range(StillMin, StillMax);
                    trembleDuration = Range(TrembleMin, TrembleMax);
                    // El lanzamiento cubre todo el ciclo para que la media quede en Decision.Speed.
                    lungeFactor = (Duration + stillDuration + trembleDuration) / Duration;
                    break;
                case WitchJerkPhase.Still: Duration = stillDuration; break;
                case WitchJerkPhase.Tremble: Duration = moving ? trembleDuration : Range(IdleTrembleMin, IdleTrembleMax); break;
                default: Duration = Range(IdleGapMin, IdleGapMax); break;
            }
        }

        private void Output()
        {
            float progress = Duration > 0f ? Mathf.Clamp01(elapsed / Duration) : 0f;
            switch (Phase)
            {
                case WitchJerkPhase.Still:
                    (SpeedFactor, AnimatorSpeed, Tremble) = (0f, StillAnimator, 0f);
                    break;
                case WitchJerkPhase.Tremble:
                    float amount = moving ? Mathf.Lerp(0.4f, 1f, progress) : Mathf.Sin(progress * Mathf.PI);
                    (SpeedFactor, AnimatorSpeed, Tremble) = (0f, TrembleAnimator, amount);
                    break;
                case WitchJerkPhase.Lunge:
                    (SpeedFactor, AnimatorSpeed, Tremble) = (lungeFactor, LungeAnimator, 0f);
                    break;
                default:
                    (SpeedFactor, AnimatorSpeed, Tremble) = (1f, 1f, 0f);
                    break;
            }
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
