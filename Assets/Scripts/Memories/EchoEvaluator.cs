using UnityEngine;

namespace TiffanyGame
{
    public struct EchoPose
    {
        public bool Visible;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    public struct EchoFrame
    {
        public EchoPose Girl;
        public EchoPose Tyler;
        public int Subtitle;
        public int FirstSound;
        public int SoundCount;
        public float BlackOpacity;
    }

    public sealed class EchoEvaluator
    {
        readonly EchoData data;
        int nextSound;

        public EchoEvaluator(EchoData data) => this.data = data;

        public void Reset() => nextSound = 0;

        public EchoFrame Evaluate(float time)
        {
            var frame = new EchoFrame
            {
                Girl = Pose(data.girl, time),
                Tyler = Pose(data.tyler, time),
                Subtitle = -1,
                FirstSound = nextSound,
                BlackOpacity = BlackOpacity(data.blackout, time)
            };
            for (int i = 0; i < data.subtitles.Length; i++)
            {
                var line = data.subtitles[i];
                if (time >= line.time && time < line.time + line.duration) { frame.Subtitle = i; break; }
            }
            while (nextSound < data.sounds.Length && data.sounds[nextSound].time <= time) nextSound++;
            frame.SoundCount = nextSound - frame.FirstSound;
            return frame;
        }

        public static EchoPose Pose(EchoTrack track, float time)
        {
            var pose = new EchoPose
            {
                Visible = time >= track.appear && time < track.disappear,
                Position = track.position,
                Rotation = Quaternion.Euler(track.rotation),
                Scale = track.scale
            };
            var points = track.points;
            if (points.Length == 0) return pose;
            var left = points[0];
            var right = left;
            for (int i = 1; i < points.Length; i++)
            {
                right = points[i];
                if (time < right.time) break;
                left = right;
            }
            float t = right.time > left.time ? Mathf.InverseLerp(left.time, right.time, time) : 0f;
            pose.Position = Vector3.Lerp(left.position, right.position, t);
            pose.Rotation = Quaternion.Slerp(Quaternion.Euler(left.rotation), Quaternion.Euler(right.rotation), t);
            pose.Scale = Vector3.Lerp(left.scale, right.scale, t);
            return pose;
        }

        public static float BlackOpacity(EchoBlackout blackout, float time)
        {
            if (blackout == null || blackout.duration <= 0f || time < blackout.time) return 0f;
            return Mathf.Clamp01((time - blackout.time) / blackout.duration);
        }

        public static float AudioEnvelope(float time, float duration, float fadeIn, float fadeOut)
        {
            if (time < 0f || time >= duration) return 0f;
            float attack = fadeIn > 0f ? Mathf.Clamp01(time / fadeIn) : 1f;
            float release = fadeOut > 0f ? Mathf.Clamp01((duration - time) / fadeOut) : 1f;
            return Mathf.Min(attack, release);
        }
    }
}
