namespace TiffanyGame
{
    public enum MemorySlideshowStage { FadeOut, Page, FadeIn, Finished }

    public sealed class MemorySlideshow
    {
        public const float FadeSeconds = 0.4f;
        public const float PageSeconds = 4f;
        float stageTime;

        public MemoryData Memory { get; }
        public EchoAudioEvaluator Audio { get; }
        public MemorySlideshowStage Stage { get; private set; }
        public int PageIndex { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished => Stage == MemorySlideshowStage.Finished;
        public bool PageVisible => Stage == MemorySlideshowStage.Page;
        public float BlackOpacity => Stage == MemorySlideshowStage.FadeOut ? stageTime / FadeSeconds
            : Stage == MemorySlideshowStage.FadeIn ? 1f - stageTime / FadeSeconds : Finished ? 0f : 1f;

        public MemorySlideshow(MemoryData memory, EchoAudioEvaluator audio = null)
        {
            Memory = memory;
            Audio = audio;
            Stage = memory?.drawings == null || memory.drawings.Length == 0
                ? MemorySlideshowStage.Finished : MemorySlideshowStage.FadeOut;
            Audio?.Reset();
            if (!Finished) Audio?.Evaluate(float.NegativeInfinity, 0f);
        }

        public void Tick(float seconds, bool advance = false)
        {
            if (Finished || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) return;
            float previous = Elapsed;
            bool manual = advance && PageVisible;
            float remaining = seconds;
            while (!Finished)
            {
                float duration = PageVisible ? PageSeconds : FadeSeconds;
                float step = System.Math.Min(remaining, duration - stageTime);
                stageTime += step;
                Elapsed += step;
                remaining -= step;
                if (stageTime < duration) break;
                NextStage();
                manual = false;
                if (remaining <= 0f) break;
            }
            if (manual) NextStage();
            Audio?.Evaluate(previous, Elapsed);
        }

        void NextStage()
        {
            stageTime = 0f;
            if (Stage == MemorySlideshowStage.FadeOut) Stage = MemorySlideshowStage.Page;
            else if (Stage == MemorySlideshowStage.FadeIn) Stage = MemorySlideshowStage.Finished;
            else if (PageIndex + 1 < Memory.drawings.Length) PageIndex++;
            else Stage = MemorySlideshowStage.FadeIn;
        }
    }
}
