using UnityEngine;
namespace TiffanyGame
{
    public enum BackgroundCue { Silent, Title, Opening, House, Ending }
    public enum BackgroundTrack { Theme, Finale }
    public sealed class BackgroundMusicRules
    {
        public const float FadeIn = 3f, FadeOut = 4f, TensionFadeOut = 1f, TensionRest = 6f;
        public BackgroundCue Cue { get; private set; }
        public BackgroundTrack Track { get; private set; }
        public float Volume { get; private set; }
        public bool Paused { get; private set; }
        public bool OpeningOver { get; private set; }
        private bool openingVisited;
        private float rest;
        public void Tick(float dt, InterfaceScreen screen, int clearing, int lastClearing, bool tension)
        {
            bool playing = screen == InterfaceScreen.Playing;
            Paused = screen == InterfaceScreen.Paused || screen == InterfaceScreen.Notebook;
            Cue = screen == InterfaceScreen.Title ? BackgroundCue.Title
                : screen == InterfaceScreen.Ending ? BackgroundCue.Ending
                : playing && lastClearing > 0 && clearing == lastClearing ? BackgroundCue.House
                : playing && !OpeningOver && clearing == 0 ? BackgroundCue.Opening : BackgroundCue.Silent;
            if (Paused || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (playing)
            {
                if (clearing == 0) openingVisited = true;
                else if (openingVisited || clearing >= 1) OpeningOver = true;
                if (tension) rest = TensionRest;
            }
            float waiting = playing && !tension ? Mathf.Min(rest, dt) : 0f;
            rest -= waiting;
            if (Cue != BackgroundCue.Silent && waiting > 0f)
            {
                Volume = Mathf.MoveTowards(Volume, 0f, waiting / TensionFadeOut);
                dt -= waiting;
            }
            BackgroundTrack next = Cue == BackgroundCue.House || Cue == BackgroundCue.Ending ? BackgroundTrack.Finale : BackgroundTrack.Theme;
            bool audible = Cue != BackgroundCue.Silent;
            if (audible && Volume == 0f) Track = next;
            bool blocked = playing && (tension || rest > 0f);
            float target = audible && !blocked && Track == next ? 1f : 0f;
            Volume = Mathf.MoveTowards(Volume, target, dt / (target > Volume ? FadeIn : playing && tension ? TensionFadeOut : FadeOut));
            if (audible && Volume == 0f) Track = next;
        }
    }
}
