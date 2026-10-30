using UnityEngine;
namespace TiffanyGame
{
    public sealed class WitchAudio : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public AudioSource fireLoop, breathLoop, oneShot;
        public AudioClip land, crack, whoosh;
        public AudioClip[] cackles;
        public PlayerFootsteps footsteps;
        private AudioClip[][] stepClips;
        private FootstepSounds stepSounds;
        private readonly WitchSoundRules rules = new WitchSoundRules();
        private bool paused;
        private void Start()
        {
            if (footsteps == null && player != null) footsteps = player.GetComponent<PlayerFootsteps>();
            if (footsteps == null) return;
            stepClips = new[] { footsteps.dirt, footsteps.grass, footsteps.slab,
                footsteps.gravel, footsteps.leaves, footsteps.branches };
            var counts = new int[stepClips.Length];
            for (int i = 0; i < counts.Length; i++) counts[i] = stepClips[i]?.Length ?? 0;
            stepSounds = new FootstepSounds(counts, footsteps.slowVolume, footsteps.walkVolume, footsteps.runVolume);
        }
        private void Update()
        {
            if (witch == null || fireLoop == null || breathLoop == null || oneShot == null) return;
            if (Time.timeScale == 0f)
            {
                fireLoop.Pause();
                breathLoop.Pause();
                paused = true;
                return;
            }
            Vector3 offset = player != null ? player.position - transform.position : Vector3.zero;
            offset.y = 0f;
            WitchDecision decision = witch.Decision;
            decision.Speed = witch.EffectiveSpeed;
            rules.Tick(Time.deltaTime, decision, player != null ? offset.magnitude : float.PositiveInfinity);
            UpdateLoop(fireLoop, rules.FireVolume);
            UpdateLoop(breathLoop, rules.BreathVolume);
            paused = false;
            if (rules.Landed) Play(land, "Landed");
            if (rules.Crack) Play(crack, "Crack");
            if (rules.Whoosh) Play(whoosh, "Whoosh");
            if (rules.Cackle) Play(cackles != null && rules.CackleClip < cackles.Length ? cackles[rules.CackleClip] : null, "Cackle");
            if (rules.Step && stepSounds != null && rules.StepAttenuation > 0f)
            {
                var step = ChooseStep();
                if (step.Clip != null) oneShot.PlayOneShot(step.Clip, step.Volume);
            }
        }
        private (AudioClip Clip, float Volume) ChooseStep()
        {
            var ground = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                out var hit, 1.5f, witch.groundMask, QueryTriggerInteraction.Ignore)
                ? GroundSurface.TypeOf(hit.collider) : GroundType.Dirt;
            var sound = stepSounds.Choose(ground, Gait.Walk, Random.value);
            return (sound.Index >= 0 ? stepClips[(int)sound.Ground][sound.Index] : null,
                sound.Volume * rules.StepAttenuation);
        }
        private void UpdateLoop(AudioSource source, float volume)
        {
            source.volume = volume;
            if (paused) source.UnPause();
            if (volume > 0f && !source.isPlaying) source.Play();
            else if (volume <= 0f && source.isPlaying) source.Stop();
        }
        private void Play(AudioClip clip, string message = null)
        {
            if (clip != null) oneShot.PlayOneShot(clip, rules.OneShotVolume);
            if (message != null) Debug.Log($"{GameInfo.LogPrefix} Bruja suena: {message}");
        }
        private void OnDisable()
        {
            if (fireLoop != null) fireLoop.Stop();
            if (breathLoop != null) breathLoop.Stop();
            if (oneShot != null) oneShot.Stop();
            paused = false;
        }
    }
}
