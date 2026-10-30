using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class EchoPlayer : MonoBehaviour
    {
        public TextAsset echoAsset;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public AudioSource[] sources = Array.Empty<AudioSource>();

        AudioClip[] soundClips;
        MemorySlideshow slideshow;
        bool loaded;

        public EchoData Data { get; private set; }
        public EchoAudioEvaluator Audio { get; private set; }
        public bool IsPlaying { get; private set; }
        public float Elapsed => slideshow != null ? slideshow.Elapsed : 0f;

        void Awake() => Load();

        public EchoAudioEvaluator PrepareAudio() => Load() ? Audio : null;

        bool Load()
        {
            if (loaded) return Audio != null;
            loaded = true;
            if (echoAsset == null) return Invalid("falta el TextAsset");
            if (!EchoData.TryLoad(echoAsset.text, out var data, out string error)) return Invalid(error);
            soundClips = new AudioClip[data.sounds.Length];
            var clipLengths = new float[data.sounds.Length];
            for (int i = 0; i < data.sounds.Length; i++)
            {
                var sound = data.sounds[i];
                for (int j = 0; j < clips.Length; j++)
                    if (clips[j] != null && clips[j].name == sound.clip) { soundClips[i] = clips[j]; break; }
                if (soundClips[i] == null) return Invalid($"no se encontró Assets/Audio/Sfx/{sound.clip}.wav");
                clipLengths[i] = soundClips[i].length;
            }
            if (data.sounds.Length > 0 && sources.Length == 0) return Invalid("el pool requiere al menos un AudioSource");
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] == null) return Invalid($"falta el AudioSource {i}");
            for (int i = 0; i < sources.Length; i++) sources[i].ignoreListenerPause = true;
            Audio = new EchoAudioEvaluator(data.sounds, clipLengths, sources.Length);
            Data = data;
            return true;
        }

        bool Invalid(string reason)
        {
            Debug.LogWarning($"{GameInfo.LogPrefix} Eco '{(echoAsset != null ? echoAsset.name : name)}' omitido: {reason}.");
            return false;
        }

        public void Play(MemorySlideshow presentation)
        {
            Stop();
            if (presentation.Audio == null || presentation.Audio != Audio) return;
            slideshow = presentation;
            IsPlaying = true;
            ApplyAudio();
        }

        public void ApplyAudio()
        {
            if (!IsPlaying) return;
            if (slideshow.Finished) { Stop(); return; }
            for (int i = 0; i < sources.Length; i++)
            {
                var plan = Audio[i];
                var source = sources[i];
                if (plan.Stop) source.Stop();
                if (plan.Active) source.volume = plan.Volume;
                if (plan.Play)
                {
                    source.clip = soundClips[plan.SoundIndex];
                    source.pitch = plan.Pitch;
                    source.panStereo = plan.PanStereo;
                    source.time = plan.ClipTime;
                    source.PlayDelayed(plan.Delay);
                }
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].Stop();
            slideshow = null;
        }

        void OnDisable() => Stop();
    }
}
