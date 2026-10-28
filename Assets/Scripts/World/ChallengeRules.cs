namespace TiffanyGame
{
    public enum ChallengeKind { Candles, Distract, Vanish, Hut }

    public sealed class ChallengeRules
    {
        public const float PassageLength = 4.4f;
        public ChallengeKind Kind { get; }
        public bool NeedsCandles => Kind == ChallengeKind.Candles || Kind == ChallengeKind.Hut;
        public bool NeedsDistraction => Kind == ChallengeKind.Distract || Kind == ChallengeKind.Hut;
        public bool NeedsVanish => Kind == ChallengeKind.Vanish || Kind == ChallengeKind.Hut;
        public int LitMask { get; private set; }
        public bool Crossing { get; private set; }
        public bool Completed { get; private set; }
        bool approached;

        public ChallengeRules(ChallengeKind kind) => Kind = kind;

        public bool GateOpen(int index) => Completed || (LitMask & (1 << index)) != 0;

        public bool Ready(bool distracted, bool vanished) => (!NeedsCandles || LitMask == 3)
            && (!NeedsDistraction || distracted) && (!NeedsVanish || vanished);

        public static bool CanRestoreLure(bool atCheckpoint, bool used, bool investigating, float age) =>
            atCheckpoint && used && !investigating && age >= 0.5f;

        public void Tick(float progress, bool inLane, int litMask, bool distracted, bool vanished)
        {
            if (Completed) return;
            LitMask |= litMask & 3;
            if (progress < 0f)
            {
                approached = true;
                Crossing = false;
                return;
            }
            bool ready = inLane && Ready(distracted, vanished);
            if (approached)
            {
                Crossing = ready;
                approached = false;
            }
            else Crossing &= ready;
            if (progress >= PassageLength && Crossing) Completed = true;
        }

        public void ResetAttempt()
        {
            if (Completed) return;
            LitMask = 0;
            Crossing = approached = false;
        }
    }
}
