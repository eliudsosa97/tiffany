using UnityEngine;

namespace TiffanyGame
{
    public struct BirdBob
    {
        public enum Motion { Still, Peck, Turn, Hop }

        public readonly struct Pose
        {
            public readonly Vector3 Offset;
            public readonly Quaternion Rotation;

            public Pose(Vector3 offset, Quaternion rotation)
            {
                Offset = offset;
                Rotation = rotation;
            }
        }

        public Motion Phase { get; private set; }
        public float Duration { get; private set; }
        public Pose Current { get; private set; }
        uint random;
        float elapsed;
        float angle;
        int gestures;

        public BirdBob(int seed)
        {
            this = default;
            random = unchecked((uint)seed) ^ 0x9E3779B9u;
            if (random == 0) random = 1;
            Duration = Range(2f, 6f);
            Current = new Pose(Vector3.zero, Quaternion.identity);
        }

        public Pose Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return Current;
            elapsed += Mathf.Min(dt, 60f);
            while (elapsed >= Duration)
            {
                elapsed -= Duration;
                if (Phase == Motion.Still)
                {
                    gestures++;
                    Phase = gestures % 5 == 0 ? Motion.Hop : Range(0f, 1f) < 0.5f ? Motion.Peck : Motion.Turn;
                    Duration = Range(0.2f, 0.5f);
                    angle = Range(12f, 23f) * (Range(0f, 1f) < 0.5f ? -1f : 1f);
                }
                else
                {
                    Phase = Motion.Still;
                    Duration = Range(2f, 6f);
                }
            }
            float pulse = Phase == Motion.Still ? 0f : Mathf.Sin(Mathf.PI * elapsed / Duration);
            Vector3 offset = Phase == Motion.Hop ? Vector3.up * (0.024f * pulse) : Vector3.zero;
            Vector3 axis = Phase == Motion.Peck ? Vector3.right : Phase == Motion.Turn ? Vector3.up : Vector3.forward;
            Current = new Pose(offset, Quaternion.AngleAxis(angle * pulse, axis));
            return Current;
        }

        float Range(float min, float max)
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            return Mathf.Lerp(min, max, (random >> 8) / 16777216f);
        }
    }
}
