using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchMimicry
    {
        public const float FirstDelay = 2.5f;
        public const float MaxInterval = 9f;
        public const float MinInterval = 5f;
        public const int FullDistortionCount = 6;
        public const float MaxPitch = 0.92f;
        public const float MinPitch = 0.62f;
        public const float MinEchoDelay = 0.18f;
        public const float MaxEchoDelay = 0.45f;
        public const float MinEchoVolume = 0.25f;
        public const float MaxEchoVolume = 0.6f;

        private float remaining = FirstDelay;
        private int previousClip = -1;

        public int Count { get; private set; }
        public float Distortion => Mathf.Clamp01((float)Count / FullDistortionCount);
        public float Interval => Mathf.Lerp(MaxInterval, MinInterval, Distortion);
        public float Pitch => Mathf.Lerp(MaxPitch, MinPitch, Distortion);
        public float EchoDelay => Mathf.Lerp(MinEchoDelay, MaxEchoDelay, Distortion);
        public float EchoVolume => Mathf.Lerp(MinEchoVolume, MaxEchoVolume, Distortion);

        public bool Tick(float dt, bool voiceLure)
        {
            if (!voiceLure)
            {
                remaining = FirstDelay;
                return false;
            }
            if (dt <= 0f || float.IsNaN(dt)) return false;
            remaining -= dt;
            if (remaining > 0f) return false;
            Count++;
            remaining = Interval;
            return true;
        }

        public int ClipIndex(int clipCount)
        {
            if (clipCount <= 0) return -1;
            previousClip = (previousClip + 1) % clipCount;
            return previousClip;
        }
    }
}
