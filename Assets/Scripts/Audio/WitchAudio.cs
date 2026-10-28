using UnityEngine;
namespace TiffanyGame
{
    public sealed class WitchAudio : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public AudioSource fireLoop, breathLoop, oneShot;
        public AudioClip land, crack, whoosh;
        public AudioClip[] steps, cackles;
        private readonly WitchSoundRules rules = new WitchSoundRules();
        private bool paused;
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
            rules.Tick(Time.deltaTime, witch.Decision, player != null ? offset.magnitude : float.PositiveInfinity);
            UpdateLoop(fireLoop, rules.FireVolume);
            UpdateLoop(breathLoop, rules.BreathVolume);
            paused = false;
            if (rules.Landed) Play(land, "Landed");
            if (rules.Crack) Play(crack, "Crack");
            if (rules.Whoosh) Play(whoosh, "Whoosh");
            if (rules.Cackle) Play(cackles != null && rules.CackleClip < cackles.Length ? cackles[rules.CackleClip] : null, "Cackle");
            if (rules.Step && steps != null && rules.StepClip < steps.Length) Play(steps[rules.StepClip]);
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
