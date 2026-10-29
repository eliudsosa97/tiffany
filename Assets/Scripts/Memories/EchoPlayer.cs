using System;
using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    public sealed class EchoPlayer : MonoBehaviour
    {
        sealed class PropAnimation
        {
            public Transform Target;
            public Animator Animator;
            public Vector3 PreviousPosition;
            public float Speed;
            public bool Started;
        }

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
        PropAnimation[] animations;
        int speedParameterHash;
        int walkStateHash;
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
            speedParameterHash = Animator.StringToHash(TylerAnimation.SpeedParameter);
            walkStateHash = Animator.StringToHash(TylerAnimation.Walk);
            var animators = stageRoot.GetComponentsInChildren<Animator>(true);
            animations = new PropAnimation[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                Transform target = animators[i].transform;
                while (target != null && target != girl && target != tyler && Array.IndexOf(props, target) < 0)
                    target = target.parent;
                if (target != null) animations[i] = CacheAnimation(target, animators[i]);
            }
            Data = data;
            evaluator = new EchoEvaluator(data);
            return true;
        }

        PropAnimation CacheAnimation(Transform target, Animator animator)
        {
            if (animator.runtimeAnimatorController == null) return null;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].nameHash == speedParameterHash && parameters[i].type == AnimatorControllerParameterType.Float)
                    return new PropAnimation { Target = target, Animator = animator };
            return null;
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
            ApplyAnimations(previousTime);
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

        void ApplyAnimations(float previousTime)
        {
            bool reset = float.IsNegativeInfinity(previousTime);
            float dt = reset ? 0f : Elapsed - previousTime;
            for (int i = 0; i < animations.Length; i++)
            {
                var animation = animations[i];
                if (animation == null) continue;
                var animator = animation.Animator;
                if (reset || !animator.isActiveAndEnabled) animation.Started = false;
                if (!animator.isActiveAndEnabled) continue;
                Vector3 position = animation.Target.position;
                if (!animation.Started)
                {
                    animation.PreviousPosition = position;
                    animation.Speed = 0f;
                    if (animator.HasState(0, walkStateHash)) animator.CrossFade(walkStateHash, 0.15f, 0);
                    animation.Started = true;
                }
                animation.Speed = EchoMotion.Speed(Vector3.Distance(animation.PreviousPosition, position), dt, animation.Speed);
                animation.PreviousPosition = position;
                animator.SetFloat(speedParameterHash, animation.Speed);
            }
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
