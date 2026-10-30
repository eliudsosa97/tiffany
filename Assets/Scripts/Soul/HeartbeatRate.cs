using UnityEngine;

namespace TiffanyGame
{
    public sealed class HeartbeatRate
    {
        public const float NearDistance = 3f;
        public const float Audible = 0.3f;
        public const float MinBpm = 70f;
        public const float MaxBpm = 140f;
        public const float MinVolume = 0.25f;
        public const float MaxVolume = 1f;
        public const float SecondsPerMinute = 60f;
        public const int LogBpmChange = 10;
        float remaining;

        public static float WitchDistance(bool active, WitchForm form, float horizontalDistance) =>
            active && form == WitchForm.Grounded ? horizontalDistance : float.PositiveInfinity;

        public static float Intensity(float panicLevel, float witchDistance) =>
            Mathf.Max(Mathf.Clamp01(panicLevel), Mathf.InverseLerp(PanicModel.WitchRadius, NearDistance, witchDistance));

        public static float BeatsPerMinute(float intensity) => intensity < Audible ? 0f
            : Mathf.Lerp(MinBpm, MaxBpm, Mathf.InverseLerp(Audible, 1f, intensity));

        public static float Volume(float intensity) => intensity < Audible ? 0f
            : Mathf.Lerp(MinVolume, MaxVolume, Mathf.InverseLerp(Audible, 1f, intensity));

        public static bool ShouldLog(float bpm, int previous) => Mathf.Abs(Mathf.RoundToInt(bpm) - previous) > LogBpmChange;

        public bool Tick(float dt, float bpm)
        {
            if (bpm <= 0f)
            {
                remaining = 0f;
                return false;
            }
            if (dt <= 0f) return false;
            remaining -= dt;
            if (remaining > 0f) return false;
            remaining = SecondsPerMinute / bpm;
            return true;
        }
    }
}
