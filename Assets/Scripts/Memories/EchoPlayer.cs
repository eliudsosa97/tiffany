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
        public Collider ground;
        public Transform girl;
        public Transform tyler;
        public Transform[] props = Array.Empty<Transform>();
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public AudioSource[] sources = Array.Empty<AudioSource>();

        EchoEvaluator evaluator;
        EchoAudioEvaluator audioEvaluator;
        AudioClip[] soundClips;
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
            audioEvaluator = new EchoAudioEvaluator(data.sounds, clipLengths, sources.Length);
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
            audioEvaluator.Reset();
            IsPlaying = true;
            gameInterface.BeginEcho();
            stageRoot.SetActive(true);
            ApplyFrame(float.NegativeInfinity);
            return true;
        }

        void Update()
        {
            if (!IsPlaying || gameInterface.IsFrozen) return;
            float previousTime = Elapsed;
            Elapsed = Mathf.Min(Data.duration, Elapsed + Time.deltaTime);
            if (Elapsed >= Data.duration) { Stop(); return; }
            ApplyFrame(previousTime);
        }

        void ApplyFrame(float previousTime)
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
            audioEvaluator.Evaluate(previousTime, Elapsed);
            ApplyAudio();
        }

        public void ApplyPose(Transform target, EchoPose pose)
        {
            if (target.gameObject.activeSelf != pose.Visible) target.gameObject.SetActive(pose.Visible);
            target.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
            target.localScale = pose.Scale;
            if (ground == null || target.parent != stageRoot.transform) return;
            Vector3 position = stageRoot.transform.TransformPoint(new Vector3(pose.Position.x, 0f, pose.Position.z));
            if (ground.Raycast(new Ray(position + Vector3.up * 30f, Vector3.down), out var hit, 60f))
                target.position += Vector3.up * (hit.point.y - position.y);
        }

        void ApplyAudio()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var plan = audioEvaluator[i];
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
            if (IsPlaying && gameInterface != null) gameInterface.EndEcho();
            IsPlaying = false;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].Stop();
            audioEvaluator?.Reset();
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
