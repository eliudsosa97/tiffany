using System;

namespace TiffanyGame
{
    public sealed class StorySequence
    {
        public const int LineCount = 3;
        public const float LineSeconds = 5f;
        public const float FadeSeconds = 0.5f;
        float elapsed;

        public int Index { get; private set; }
        public bool IsFinished => Index == LineCount;
        public float Alpha => IsFinished ? 0f : Math.Min(1f,
            Math.Min(elapsed, LineSeconds - elapsed) / FadeSeconds);

        public void Tick(float dt)
        {
            if (dt <= 0f || IsFinished) return;
            elapsed += dt;
            while (elapsed >= LineSeconds && !IsFinished)
            {
                elapsed -= LineSeconds;
                Index++;
            }
        }

        public void Advance()
        {
            if (!IsFinished) Index++;
            elapsed = 0f;
        }

        public static bool ShouldSkip(string[] args, bool automatic)
        {
            if (automatic || SaveRules.IsAutomaticRun(args)) return true;
            foreach (string arg in args)
                if (arg == "-runTests" || arg == "-capturevideo" || arg == "-motiondemo"
                    || arg == "-witchdemo" || arg == "-fogdemo") return true;
            return false;
        }
    }
}
