using System;
namespace TiffanyGame
{
    public enum UnseenZone { Forest, Trail, Town }
    public enum UnseenSound { Branch, Steps, Whisper, Knock, Name }
    public sealed class UnseenForestRules
    {
        public const float ForestMinInterval = 50f, ForestMaxInterval = 80f;
        public const float TrailMinInterval = 80f, TrailMaxInterval = 120f;
        public const float TownMinInterval = 90f, TownMaxInterval = 120f;
        public const float ChaseCooldown = 12f, OtherScareGap = 8f;
        public const float NameCooldown = 150f;
        public const float MinDistance = 15f, MaxDistance = 30f, FrontHalfAngle = 70f, HalfTurn = 180f;
        public const int SoundCount = 4;
        public UnseenSound Sound { get; private set; }
        public float Distance { get; private set; }
        public float Angle { get; private set; }
        public float Remaining { get; private set; }
        private readonly Random random;
        private UnseenZone zone;
        private bool started;
        private int last = -1;
        private float cooldown;
        private float nameCooldown;
        public UnseenForestRules(int seed) => random = new Random(seed);
        public bool Tick(float dt, UnseenZone zone, bool chasing, float secondsSinceOtherScare, bool nameAvailable)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return false;
            nameCooldown = Math.Max(0f, nameCooldown - dt);
            float min = zone == UnseenZone.Forest ? ForestMinInterval : zone == UnseenZone.Trail ? TrailMinInterval : TownMinInterval;
            float max = zone == UnseenZone.Forest ? ForestMaxInterval : zone == UnseenZone.Trail ? TrailMaxInterval : TownMaxInterval;
            if (!started) { Remaining = Range(min, max); started = true; }
            else if (this.zone != zone) Remaining = Math.Min(Remaining, max);
            this.zone = zone;
            if (chasing) { cooldown = ChaseCooldown; return false; }
            cooldown = Math.Max(0f, cooldown - dt);
            Remaining = Math.Max(0f, Remaining - dt);
            if (Remaining > 0f || cooldown > 0f || secondsSinceOtherScare < OtherScareGap) return false;
            bool useName = nameAvailable && nameCooldown <= 0f && last != (int)UnseenSound.Name && random.Next(6) == 0;
            bool skipLast = last >= 0 && last < SoundCount;
            int next = useName ? (int)UnseenSound.Name : random.Next(skipLast ? SoundCount - 1 : SoundCount);
            if (!useName && skipLast && next >= last) next++;
            if (useName) nameCooldown = NameCooldown;
            Sound = (UnseenSound)(last = next);
            Distance = Range(MinDistance, MaxDistance);
            Angle = Range(FrontHalfAngle, HalfTurn * 2f - FrontHalfAngle);
            if (Angle > HalfTurn) Angle -= HalfTurn * 2f;
            Remaining = Range(min, max);
            return true;
        }
        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
