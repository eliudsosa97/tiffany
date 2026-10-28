using System;
using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    public sealed class EchoPlayer : MonoBehaviour
    {
        public TextAsset echoAsset;
        public GameInterface gameInterface;
        public Image blackout;
        public GameObject stageRoot;
        public Transform girl;
        public Transform tyler;
        public Transform[] props = Array.Empty<Transform>();
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public AudioSource[] sources = Array.Empty<AudioSource>();

        struct Voice
        {
            public EchoSound Sound;
            public float Start;
            public float Duration;
            public float Volume;
        }

        EchoEvaluator evaluator;
        AudioClip[] soundClips;
        Voice[] voices;
        int subtitleIndex = -1;
        bool loaded;

        public EchoData Data { get; private set; }
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }

        void Awake()
        {
            Load();
            Hide();
        }

        bool Load()
        {
            if (loaded) return evaluator != null;
            loaded = true;
            if (echoAsset == null) return Invalid("falta el TextAsset");
            if (!EchoData.TryLoad(echoAsset.text, out var data, out string error)) return Invalid(error);
            if (stageRoot == null || girl == null || tyler == null || blackout == null || gameInterface == null || props.Length != data.props.Length)
                return Invalid("faltan referencias de figuras, utilería o interfaz");
            for (int i = 0; i < props.Length; i++)
                if (props[i] == null) return Invalid($"falta la utilería {i}");
            soundClips = new AudioClip[data.sounds.Length];
            int voiceCount = 0;
            for (int i = 0; i < data.sounds.Length; i++)
            {
                var sound = data.sounds[i];
                for (int j = 0; j < clips.Length; j++)
                    if (clips[j] != null && clips[j].name == sound.clip) { soundClips[i] = clips[j]; break; }
                if (soundClips[i] == null) return Invalid($"no se encontró Assets/Audio/Sfx/{sound.clip}.wav");
                voiceCount += 1 + sound.copies.Length;
            }
            if (sources.Length < voiceCount) return Invalid($"el pool requiere {voiceCount} AudioSource");
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] == null) return Invalid($"falta el AudioSource {i}");
            voices = new Voice[sources.Length];
            Data = data;
            evaluator = new EchoEvaluator(data);
            return true;
        }

        bool Invalid(string reason)
        {
            Debug.LogWarning($"{GameInfo.LogPrefix} Eco '{(echoAsset != null ? echoAsset.name : name)}' omitido: {reason}.");
            return false;
        }

        public bool Play()
        {
            Stop();
            if (!Load()) return false;
            Elapsed = 0f;
            subtitleIndex = -1;
            evaluator.Reset();
            IsPlaying = true;
            gameInterface.BeginEcho();
            stageRoot.SetActive(true);
            ApplyFrame();
            return true;
        }

        void Update()
        {
            if (!IsPlaying || gameInterface.IsFrozen) return;
            Elapsed = Mathf.Min(Data.duration, Elapsed + Time.deltaTime);
            if (Elapsed >= Data.duration) { Stop(); return; }
            ApplyFrame();
        }

        void ApplyFrame()
        {
            var frame = evaluator.Evaluate(Elapsed);
            ApplyPose(girl, frame.Girl);
            ApplyPose(tyler, frame.Tyler);
            for (int i = 0; i < props.Length; i++) ApplyPose(props[i], EchoEvaluator.Pose(Data.props[i], Elapsed));
            if (subtitleIndex != frame.Subtitle)
            {
                subtitleIndex = frame.Subtitle;
                var line = subtitleIndex >= 0 ? Data.subtitles[subtitleIndex] : null;
                gameInterface.ReplaceSubtitle(line?.text, line != null ? line.time + line.duration - Elapsed : 0f);
            }
            blackout.color = new Color(0f, 0f, 0f, frame.BlackOpacity);
            blackout.gameObject.SetActive(frame.BlackOpacity > 0f);
            for (int i = 0; i < frame.SoundCount; i++) StartSound(frame.FirstSound + i);
            UpdateAudio();
        }

        static void ApplyPose(Transform target, EchoPose pose)
        {
            if (target.gameObject.activeSelf != pose.Visible) target.gameObject.SetActive(pose.Visible);
            target.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
            target.localScale = pose.Scale;
        }

        void StartSound(int index)
        {
            var sound = Data.sounds[index];
            Schedule(sound, soundClips[index], 0f, sound.pitch, sound.volume, sound.panStereo);
            for (int i = 0; i < sound.copies.Length; i++)
            {
                var copy = sound.copies[i];
                Schedule(sound, soundClips[index], copy.delay, copy.pitch, copy.volume, copy.panStereo);
            }
        }

        void Schedule(EchoSound sound, AudioClip clip, float delay, float pitch, float volume, float pan)
        {
            float start = sound.time + delay;
            float duration = Mathf.Min(sound.duration, clip.length / pitch);
            if (Elapsed >= start + duration) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i].Sound != null) continue;
                voices[i] = new Voice { Sound = sound, Start = start, Duration = duration, Volume = volume };
                var source = sources[i];
                source.clip = clip;
                source.pitch = pitch;
                source.panStereo = pan;
                source.volume = volume * EchoEvaluator.AudioEnvelope(Elapsed - start, duration, sound.fadeIn, sound.fadeOut);
                source.time = Mathf.Max(0f, Elapsed - start) * pitch;
                source.PlayDelayed(Mathf.Max(0f, start - Elapsed));
                return;
            }
        }

        void UpdateAudio()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (voice.Sound == null) continue;
                float age = Elapsed - voice.Start;
                if (age >= voice.Duration)
                {
                    sources[i].Stop();
                    voices[i] = default;
                }
                else sources[i].volume = voice.Volume * EchoEvaluator.AudioEnvelope(age, voice.Duration, voice.Sound.fadeIn, voice.Sound.fadeOut);
            }
        }

        public void Stop()
        {
            if (IsPlaying && gameInterface != null) gameInterface.EndEcho();
            IsPlaying = false;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].Stop();
            if (voices != null) Array.Clear(voices, 0, voices.Length);
            Hide();
        }

        void Hide()
        {
            if (stageRoot != null) stageRoot.SetActive(false);
            if (blackout != null) blackout.gameObject.SetActive(false);
        }

        void OnDisable() => Stop();
    }
}
