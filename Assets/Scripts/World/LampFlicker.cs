using System;

namespace TiffanyGame
{
    public sealed class LampFlicker
    {
        public const float MinIntensity = 0.12f;
        public const float HumAmplitude = 0.06f;
        public const float HumFrequency = 18f;
        public const float MinDropInterval = 4f;
        public const float MaxDropInterval = 12f;
        public const float MinDropDuration = 0.05f;
        public const float MaxDropDuration = 0.3f;
        public const float ReboundIntensity = 0.65f;

        readonly Random random;
        readonly uint noiseSeed;
        double time;
        double dropStart;
        float dropDuration;
        int rebounds;
        float intensity;

        public LampFlicker(int seed)
        {
            random = new Random(seed);
            noiseSeed = unchecked((uint)seed);
            ScheduleDrop();
            intensity = 1f - HumAmplitude * Noise(0);
        }

        public float Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return intensity;
            time += dt;
            while (time >= dropStart + dropDuration) ScheduleDrop();
            float hum = 1f - HumAmplitude * Noise(time * HumFrequency);
            if (time < dropStart) return intensity = hum;
            int segments = 2 * rebounds + 2;
            double phase = (time - dropStart) / dropDuration * segments;
            int segment = (int)phase;
            float from = segment == 0 ? 1f : segment % 2 == 0 ? ReboundIntensity : 0f;
            float to = segment == segments - 1 ? 1f : segment % 2 == 0 ? 0f : ReboundIntensity;
            float blend = Smooth((float)(phase - segment));
            float pulse = from + (to - from) * blend;
            return intensity = MinIntensity + (hum - MinIntensity) * pulse;
        }

        void ScheduleDrop()
        {
            dropStart += Range(MinDropInterval, MaxDropInterval);
            dropDuration = Range(MinDropDuration, MaxDropDuration);
            rebounds = random.Next(1, 3);
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        float Noise(double position)
        {
            double cell = Math.Floor(position);
            uint index = (uint)(cell % 4294967296d);
            float a = Hash(index), b = Hash(unchecked(index + 1));
            return a + (b - a) * Smooth((float)(position - cell));
        }

        float Hash(uint index)
        {
            unchecked
            {
                uint value = index ^ noiseSeed;
                value = (value ^ (value >> 16)) * 0x7feb352d;
                value = (value ^ (value >> 15)) * 0x846ca68b;
                return ((value ^ (value >> 16)) >> 8) / 16777215f;
            }
        }

        static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}
