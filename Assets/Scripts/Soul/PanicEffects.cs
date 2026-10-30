using UnityEngine;

namespace TiffanyGame
{
    public readonly struct PanicCues
    {
        public readonly bool FakeStep;
        public readonly bool Whisper;
        public readonly bool Sob;
        public readonly float LightScale;

        public PanicCues(bool fakeStep, bool whisper, bool sob, float lightScale)
        {
            FakeStep = fakeStep;
            Whisper = whisper;
            Sob = sob;
            LightScale = lightScale;
        }
    }

    public sealed class PanicEffects
    {
        public const float FakeStepMinInterval = 4f;
        public const float FakeStepMaxInterval = 8f;
        public const float WhisperMinInterval = 6f;
        public const float WhisperMaxInterval = 10f;
        public const float SobInterval = 4f;
        public const float SobRadius = 6f;
        public const float CueGap = 2.5f;

        private readonly System.Random random;
        private float stepRemaining = -1f;
        private float whisperRemaining = -1f;
        private float sobRemaining = -1f;
        private float cueRemaining;

        public PanicEffects(System.Random random = null)
        {
            this.random = random ?? new System.Random();
        }

        public PanicCues Tick(float dt, PanicStage stage, float time, bool hidden)
        {
            dt = Mathf.Max(0f, dt);
            cueRemaining = Mathf.Max(0f, cueRemaining - dt);
            bool step = TickCue(dt, stage >= PanicStage.Steps, ref stepRemaining,
                FakeStepMinInterval, FakeStepMaxInterval);
            bool whisper = TickCue(dt, stage >= PanicStage.FalseFires, ref whisperRemaining,
                WhisperMinInterval, WhisperMaxInterval);
            bool sob = TickCue(dt, stage >= PanicStage.Children && !hidden,
                ref sobRemaining, SobInterval, SobInterval);
            if (cueRemaining > 0f) step = whisper = sob = false;
            else if (sob)
            {
                step = whisper = false;
                sobRemaining = SobInterval;
            }
            else if (whisper)
            {
                step = false;
                whisperRemaining = NextInterval(WhisperMinInterval, WhisperMaxInterval);
            }
            else if (step) stepRemaining = NextInterval(FakeStepMinInterval, FakeStepMaxInterval);
            if (step || whisper || sob) cueRemaining = CueGap;
            float scale = stage >= PanicStage.Children
                ? Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(time * 13f, 0.37f)) : 1f;
            return new PanicCues(step, whisper, sob, scale);
        }

        private bool TickCue(float dt, bool active, ref float remaining, float min, float max)
        {
            if (!active)
            {
                remaining = -1f;
                return false;
            }
            if (remaining < 0f) remaining = NextInterval(min, max);
            remaining = Mathf.Max(0f, remaining - dt);
            return remaining <= 0f;
        }

        private float NextInterval(float min, float max) =>
            min == max ? min : min + (float)random.NextDouble() * (max - min);
    }
}
