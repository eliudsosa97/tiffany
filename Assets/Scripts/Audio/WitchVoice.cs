using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchVoice : MonoBehaviour
    {
        public WitchController witch;
        public AudioSource source;
        public AudioSource echo;
        public AudioClip[] barks;
        public TylerController tyler;
        public int Count => mimicry.Count;

        private readonly WitchMimicry mimicry = new WitchMimicry();

        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                StopAudio();
                return;
            }
            if (witch == null || source == null || echo == null || barks == null || barks.Length == 0) return;
            if (!mimicry.Tick(Time.deltaTime, witch.isActiveAndEnabled
                && witch.Decision.VoiceLure && witch.Decision.Form == WitchForm.Grounded)) return;
            AudioClip clip = barks[mimicry.ClipIndex(barks.Length)];
            if (clip == null) return;
            source.pitch = echo.pitch = mimicry.Pitch;
            source.PlayOneShot(clip);
            echo.clip = clip;
            echo.volume = mimicry.EchoVolume;
            echo.PlayDelayed(mimicry.EchoDelay);
            if (tyler != null) tyler.HearMimic();
            Debug.Log($"{GameInfo.LogPrefix} Bruja imita a Tyler ({Count})");
        }

        private void OnDisable() => StopAudio();

        private void StopAudio()
        {
            if (source != null) source.Stop();
            if (echo != null) echo.Stop();
        }
    }
}
