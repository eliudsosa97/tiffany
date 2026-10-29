using UnityEngine;
namespace TiffanyGame
{
    public sealed class BackgroundMusic : MonoBehaviour
    {
        public GameInterface gameInterface;
        public WitchChapterTracker tracker;
        public TensionMusic tension;
        public WitchController witch;
        public AudioSource source;
        public AudioClip theme, finale;
        public float maxVolume = 0.5f;
        public BackgroundMusicRules Rules => rules;
        private readonly BackgroundMusicRules rules = new BackgroundMusicRules();
        private bool paused;
        private BackgroundCue cue;
        private void Start()
        {
            if (source == null) return;
            source.ignoreListenerPause = true;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }
        private void Update()
        {
            if (gameInterface == null || source == null) return;
            int clearing = tracker != null && tracker.player != null ? RouteChapters.FindClearing(tracker.player.position, tracker.ClearingBounds) : -1;
            int lastClearing = tracker != null && tracker.clearings != null ? tracker.clearings.Length - 1 : -1;
            bool tensionActive = (tension != null && tension.Level > 0f)
                || (witch != null && witch.isActiveAndEnabled && TensionMusicRules.IsChasing(witch.Decision, witch.Profile.FollowSpeed));
            rules.Tick(Time.unscaledDeltaTime, gameInterface.Screen, clearing, lastClearing, tensionActive);
            if (rules.Paused)
            {
                if (!paused) source.Pause();
                paused = true;
            }
            else
            {
                if (paused) { source.UnPause(); paused = false; }
                AudioClip clip = rules.Track == BackgroundTrack.Theme ? theme : finale;
                float previous = source.volume;
                if (rules.Volume == 0f) source.volume = 0f;
                if (source.volume == 0f && source.clip != clip) source.clip = clip;
                source.volume = rules.Volume * maxVolume;
                if (source.volume > 0f && previous <= 0f && source.clip != null) source.Play();
                else if (source.volume <= 0f && previous > 0f) source.Stop();
            }
            if (rules.Cue == cue) return;
            cue = rules.Cue;
            Debug.Log(cue == BackgroundCue.Title ? GameInfo.LogPrefix + " Música de fondo: inicio"
                : cue == BackgroundCue.Opening ? GameInfo.LogPrefix + " Música de fondo: cementerio"
                : cue == BackgroundCue.House ? GameInfo.LogPrefix + " Música de fondo: casa"
                : cue == BackgroundCue.Ending ? GameInfo.LogPrefix + " Música de fondo: final" : GameInfo.LogPrefix + " Música de fondo: silencio");
        }
        private void OnDisable()
        {
            if (source != null) { source.Stop(); source.volume = 0f; }
            paused = false;
        }
    }
}
