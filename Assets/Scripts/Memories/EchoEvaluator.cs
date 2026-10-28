using System;
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

    public struct EchoVoicePlan
    {
        public bool Active;
        public bool Play;
        public bool Stop;
        public int SoundIndex;
        public float Delay;
        public float ClipTime;
        public float Duration;
        public float Pitch;
        public float Volume;
        public float PanStereo;
    }

    public sealed class EchoAudioEvaluator
    {
        struct VoiceEvent : IComparable<VoiceEvent>
        {
            public int SoundIndex;
            public EchoSound Sound;
            public float Start;
            public float Duration;
            public float Pitch;
            public float Volume;
            public float Pan;

            public int CompareTo(VoiceEvent other)
            {
                int order = Sound.time.CompareTo(other.Sound.time);
                if (order == 0) order = Start.CompareTo(other.Start);
                if (order == 0) order = string.CompareOrdinal(Sound.clip, other.Sound.clip);
                if (order == 0) order = Pitch.CompareTo(other.Pitch);
                if (order == 0) order = Volume.CompareTo(other.Volume);
                if (order == 0) order = Pan.CompareTo(other.Pan);
                if (order == 0) order = Duration.CompareTo(other.Duration);
                if (order == 0) order = Sound.fadeIn.CompareTo(other.Sound.fadeIn);
                if (order == 0) order = Sound.fadeOut.CompareTo(other.Sound.fadeOut);
                return order;
            }
        }

        readonly VoiceEvent[] events;
        readonly int[] activeEvents;
        readonly EchoVoicePlan[] plans;

        public EchoVoicePlan this[int voice] => plans[voice];

        public EchoAudioEvaluator(EchoSound[] sounds, float[] clipLengths, int voiceCount)
        {
            int count = 0;
            for (int i = 0; i < sounds.Length; i++) count += 1 + sounds[i].copies.Length;
            events = new VoiceEvent[count];
            activeEvents = new int[voiceCount];
            plans = new EchoVoicePlan[voiceCount];
            int next = 0;
            for (int i = 0; i < sounds.Length; i++)
            {
                var sound = sounds[i];
                Add(i, 0f, sound.pitch, sound.volume, sound.panStereo);
                for (int j = 0; j < sound.copies.Length; j++)
                {
                    var copy = sound.copies[j];
                    Add(i, copy.delay, copy.pitch, copy.volume, copy.panStereo);
                }
            }
            Array.Sort(events);
            Reset();

            void Add(int index, float delay, float pitch, float volume, float pan)
            {
                var sound = sounds[index];
                events[next++] = new VoiceEvent
                {
                    SoundIndex = index, Sound = sound, Start = sound.time + delay,
                    Duration = Mathf.Min(sound.duration, clipLengths[index] / pitch),
                    Pitch = pitch, Volume = volume, Pan = pan
                };
            }
        }

        public void Reset()
        {
            Array.Clear(plans, 0, plans.Length);
            for (int i = 0; i < activeEvents.Length; i++) activeEvents[i] = -1;
        }

        public void Evaluate(float previousTime, float time)
        {
            for (int i = 0; i < plans.Length; i++)
            {
                plans[i] = default;
                if (activeEvents[i] < 0) continue;
                var voice = events[activeEvents[i]];
                if (time < voice.Start + voice.Duration) continue;
                activeEvents[i] = -1;
                plans[i].Stop = true;
            }
            for (int i = 0; i < events.Length && activeEvents.Length > 0; i++)
            {
                var voice = events[i];
                if (voice.Sound.time <= previousTime || voice.Sound.time > time || time >= voice.Start + voice.Duration) continue;
                int slot = 0;
                for (int j = 0; j < activeEvents.Length; j++)
                {
                    if (activeEvents[j] < 0) { slot = j; break; }
                    if (events[activeEvents[j]].Start < events[activeEvents[slot]].Start) slot = j;
                }
                plans[slot].Stop |= activeEvents[slot] >= 0;
                plans[slot].Play = true;
                activeEvents[slot] = i;
            }
            for (int i = 0; i < plans.Length; i++)
            {
                if (activeEvents[i] < 0) continue;
                var voice = events[activeEvents[i]];
                float age = time - voice.Start;
                plans[i] = new EchoVoicePlan
                {
                    Active = true, Play = plans[i].Play, Stop = plans[i].Stop,
                    SoundIndex = voice.SoundIndex, Delay = Mathf.Max(0f, -age),
                    ClipTime = Mathf.Max(0f, age) * voice.Pitch, Duration = voice.Duration,
                    Pitch = voice.Pitch, PanStereo = voice.Pan,
                    Volume = voice.Volume * EchoEvaluator.AudioEnvelope(age, voice.Duration, voice.Sound.fadeIn, voice.Sound.fadeOut)
                };
            }
        }
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
