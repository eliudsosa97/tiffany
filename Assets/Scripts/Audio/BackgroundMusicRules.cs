using UnityEngine;
namespace TiffanyGame
{
    public enum BackgroundCue { Silent, Title, Memory, Ending }
    public enum BackgroundTrack { Theme, Memory, Finale }
    public sealed class BackgroundMusicRules
    {
        public const float FadeIn = 3f, FadeOut = 4f, MemoryFade = 0.5f;
        public BackgroundCue Cue { get; private set; }
        public BackgroundTrack Track { get; private set; }
        public float Volume { get; private set; }
        public bool Paused { get; private set; }
        private bool memory;
        public void PlayMemory() => memory = true;
        public void StopMemory() => memory = false;
        public void Tick(float dt, InterfaceScreen screen)
        {
            Paused = screen == InterfaceScreen.Paused || screen == InterfaceScreen.Notebook;
            if (Paused) return;
            Cue = screen == InterfaceScreen.Title ? BackgroundCue.Title
                : screen == InterfaceScreen.Ending ? BackgroundCue.Ending
                : screen == InterfaceScreen.Playing && memory ? BackgroundCue.Memory : BackgroundCue.Silent;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (screen != InterfaceScreen.Playing) memory = false;
            if (Cue == BackgroundCue.Silent)
            {
                Volume = Track == BackgroundTrack.Memory ? Mathf.MoveTowards(Volume, 0f, dt / MemoryFade) : 0f;
                return;
            }
            BackgroundTrack next = Cue == BackgroundCue.Ending ? BackgroundTrack.Finale
                : Cue == BackgroundCue.Memory ? BackgroundTrack.Memory : BackgroundTrack.Theme;
            if (Volume == 0f) Track = next;
            float target = Track == next ? 1f : 0f;
            float fade = Track == BackgroundTrack.Memory || next == BackgroundTrack.Memory ? MemoryFade
                : target > Volume ? FadeIn : FadeOut;
            Volume = Mathf.MoveTowards(Volume, target, dt / fade);
            if (Volume == 0f) Track = next;
        }
    }
}
