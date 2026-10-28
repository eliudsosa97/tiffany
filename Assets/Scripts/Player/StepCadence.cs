using System;

namespace ElVelador
{
    public class StepCadence
    {
        public const float SlowStride = 0.5f;
        public const float WalkStride = 0.7f;
        public const float RunStride = 1.1f;
        public const float StartPhase = 0.5f;

        float phase = StartPhase;

        public static float Stride(Gait gait)
        {
            return gait switch
            {
                Gait.Slow => SlowStride,
                Gait.Walk => WalkStride,
                Gait.Run => RunStride,
                _ => throw new ArgumentOutOfRangeException(nameof(gait))
            };
        }

        public bool Advance(float distance, Gait gait)
        {
            if (distance <= 0f)
            {
                phase = Math.Max(phase, StartPhase);
                return false;
            }

            phase += distance / Stride(gait);
            if (phase < 1f) return false;

            phase = (phase - 1f) % 1f;
            return true;
        }

        public void Reset()
        {
            phase = StartPhase;
        }
    }
}
