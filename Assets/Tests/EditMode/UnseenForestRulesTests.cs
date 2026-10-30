using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class UnseenForestRulesTests
    {
        private const float Delta = 0.1f;
        private UnseenForestRules rules;
        [SetUp] public void SetUp() => rules = new UnseenForestRules(18);
        private bool Tick(float dt, UnseenZone zone = UnseenZone.Forest, bool chasing = false, float sinceScare = float.PositiveInfinity) =>
            rules.Tick(dt, zone, chasing, sinceScare);
        private float Next(UnseenZone zone = UnseenZone.Forest)
        {
            for (int frame = 1; frame <= 601; frame++)
                if (Tick(Delta, zone)) return frame * Delta;
            Assert.Fail("No sonó antes del intervalo máximo.");
            return 0f;
        }
        [Test] public void SameSeedProducesIdenticalTimingSoundsDistancesAndAngles()
        {
            var other = new UnseenForestRules(18);
            int sounds = 0;
            for (int frame = 0; sounds < 200 && frame < 120200; frame++)
            {
                UnseenZone zone = (UnseenZone)((frame / 100) % 3);
                bool chasing = frame % 1000 < 50;
                bool sounded = Tick(Delta, zone, chasing, frame % 20);
                Assert.AreEqual(sounded, other.Tick(Delta, zone, chasing, frame % 20));
                if (!sounded) continue;
                sounds++;
                Assert.AreEqual((rules.Sound, rules.Distance, rules.Angle), (other.Sound, other.Distance, other.Angle));
            }
            Assert.AreEqual(200, sounds);
        }
        [TestCase(UnseenZone.Forest, 25f, 40f)]
        [TestCase(UnseenZone.Trail, 40f, 60f)]
        [TestCase(UnseenZone.Town, 45f, 60f)]
        public void TwoHundredIntervalsStayInZoneRange(UnseenZone zone, float min, float max)
        {
            for (int i = 0; i < 200; i++) Assert.That(Next(zone), Is.InRange(min, max + Delta));
        }
        [Test] public void ChaseFreezesTimerAndWaitsAtLeastTwelveSecondsAfterEnding()
        {
            Assert.IsFalse(Tick(1f));
            Assert.IsFalse(Tick(rules.Remaining - 1f));
            float remaining = rules.Remaining;
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(Tick(1f, chasing: true));
                Assert.AreEqual(remaining, rules.Remaining);
            }
            Assert.IsFalse(Tick(UnseenForestRules.ChaseCooldown - 0.5f));
            Assert.IsTrue(Tick(0.5f));
        }
        [Test] public void LongRemainingIntervalSurvivesChaseCooldown()
        {
            Assert.IsFalse(Tick(1f, UnseenZone.Town));
            float remaining = rules.Remaining;
            Assert.IsFalse(Tick(100f, UnseenZone.Town, chasing: true));
            Assert.IsFalse(Tick(UnseenForestRules.ChaseCooldown, UnseenZone.Town));
            Assert.AreEqual(remaining - UnseenForestRules.ChaseCooldown, rules.Remaining);
            Assert.IsTrue(Tick(rules.Remaining, UnseenZone.Town));
        }
        [Test] public void OtherScareBlocksUntilEightSecondsWithoutRestartingInterval()
        {
            Assert.IsFalse(Tick(100f, sinceScare: 0f));
            Assert.IsFalse(Tick(7.9f, sinceScare: UnseenForestRules.OtherScareGap - 0.1f));
            Assert.AreEqual(0f, rules.Remaining);
            Assert.IsTrue(Tick(0.1f, sinceScare: UnseenForestRules.OtherScareGap));
        }
        [Test] public void FiveHundredSoundsNeverRepeatAndCoverAllFourKinds()
        {
            var seen = new bool[UnseenForestRules.SoundCount];
            int previous = -1;
            for (int i = 0; i < 500; i++)
            {
                Assert.IsTrue(Tick(UnseenForestRules.ForestMaxInterval));
                Assert.AreNotEqual(previous, (int)rules.Sound);
                seen[previous = (int)rules.Sound] = true;
                Assert.That(rules.Distance, Is.InRange(UnseenForestRules.MinDistance, UnseenForestRules.MaxDistance));
                Assert.That(System.Math.Abs(rules.Angle), Is.InRange(UnseenForestRules.FrontHalfAngle, UnseenForestRules.HalfTurn));
            }
            CollectionAssert.AreEqual(new[] { true, true, true, true }, seen);
        }
        [Test] public void ForestToTownKeepsThirtyFiveSecondsInsteadOfExtendingInterval()
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                rules = new UnseenForestRules(seed);
                Tick(Delta);
                if (rules.Remaining < 35f) continue;
                Tick(rules.Remaining - 35f);
                Assert.AreEqual(35f, rules.Remaining, 1e-5f);
                Assert.IsFalse(Tick(Delta, UnseenZone.Town));
                Assert.AreEqual(35f - Delta, rules.Remaining, 1e-5f);
                Assert.LessOrEqual(rules.Remaining, UnseenForestRules.TownMaxInterval);
                Assert.IsTrue(Tick(rules.Remaining, UnseenZone.Town));
                return;
            }
            Assert.Fail("No se encontró un intervalo de bosque de al menos 35 s.");
        }
        [Test] public void TownToForestClampsRemainingToFortySeconds()
        {
            Assert.IsFalse(Tick(Delta, UnseenZone.Town));
            Assert.Greater(rules.Remaining, UnseenForestRules.ForestMaxInterval);
            Assert.IsFalse(Tick(Delta));
            Assert.AreEqual(UnseenForestRules.ForestMaxInterval - Delta, rules.Remaining, 1e-5f);
            Assert.IsTrue(Tick(rules.Remaining));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaChangesNeitherTimerCooldownZoneNorRandomSequence(float dt)
        {
            var other = new UnseenForestRules(18);
            Assert.IsFalse(Tick(dt, UnseenZone.Town, chasing: true));
            Assert.IsFalse(Tick(1f));
            Assert.IsFalse(other.Tick(1f, UnseenZone.Forest, false, float.PositiveInfinity));
            float remaining = rules.Remaining;
            Assert.IsFalse(Tick(dt, UnseenZone.Town, chasing: true));
            Assert.AreEqual(remaining, rules.Remaining);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(Tick(i == 0 ? remaining : UnseenForestRules.ForestMaxInterval));
                Assert.IsTrue(other.Tick(i == 0 ? remaining : UnseenForestRules.ForestMaxInterval,
                    UnseenZone.Forest, false, float.PositiveInfinity));
                Assert.AreEqual((other.Sound, other.Distance, other.Angle, other.Remaining),
                    (rules.Sound, rules.Distance, rules.Angle, rules.Remaining));
            }
        }
    }
}
