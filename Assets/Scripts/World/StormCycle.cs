using System;

namespace TiffanyGame
{
    public sealed class StormCycle
    {
        public const float MinStrikeInterval = 12f;
        public const float MaxStrikeInterval = 30f;
        public const float FlashDuration = 1f;
        public const float MinThunderDelay = 0.4f;
        public const float MaxThunderDelay = 2f;
        public const float ThunderDuration = 3f;
        const float FlashPeakLevel = 1f;
        const float FlashDipStart = 0.08f;
        const float FlashDipLevel = 0.2f;
        const float FlashReboundStart = 0.16f;
        const float FlashReboundLevel = 0.8f;
        const float FlashFadeStart = 0.26f;
        readonly Func<float> random01;
        double time;
        double thunderUntil;
        double flashAt = double.NegativeInfinity;
        double nextStrike = double.PositiveInfinity;
        double thunderAt = double.PositiveInfinity;
        bool active;

        public StormCycle(Func<float> random01) => this.random01 = random01 ?? throw new ArgumentNullException(nameof(random01));

        public StormCycle(int seed)
        {
            var random = new Random(seed);
            random01 = () => random.Next(1 << 24) / 16777216f;
        }

        public bool Active
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                nextStrike = active ? time + Range(MinStrikeInterval, MaxStrikeInterval) : double.PositiveInfinity;
                if (active) return;
                flashAt = double.NegativeInfinity;
                thunderAt = double.PositiveInfinity;
                thunderUntil = time;
                ThunderStartedThisTick = false;
            }
        }

        public bool Flashing => time - flashAt < FlashDuration;

        public float FlashLevel
        {
            get
            {
                if (!Flashing) return 0f;
                double elapsed = time - flashAt;
                if (elapsed < FlashDipStart) return FlashPeakLevel;
                if (elapsed < FlashReboundStart) return FlashDipLevel;
                if (elapsed < FlashFadeStart) return FlashReboundLevel;
                return FlashReboundLevel * (float)((FlashDuration - elapsed) / (FlashDuration - FlashFadeStart));
            }
        }

        public bool ThunderSounding => time < thunderUntil;

        public bool ThunderStartedThisTick { get; private set; }

        public void Strike() => BeginStrike();

        void BeginStrike()
        {
            flashAt = time;
            thunderAt = time + Range(MinThunderDelay, MaxThunderDelay);
            if (active) nextStrike = time + Range(MinStrikeInterval, MaxStrikeInterval);
        }

        public void Tick(float dt)
        {
            ThunderStartedThisTick = false;
            if (!(dt >= 0f) || float.IsInfinity(dt)) return;
            double end = time + dt;
            while (Math.Min(nextStrike, thunderAt) <= end)
            {
                time = Math.Min(nextStrike, thunderAt);
                if (time == thunderAt)
                {
                    thunderUntil = time + ThunderDuration;
                    thunderAt = double.PositiveInfinity;
                    ThunderStartedThisTick = true;
                }
                else BeginStrike();
            }
            time = end;
        }

        float Range(float min, float max) => min + (max - min) * random01();
    }
}
