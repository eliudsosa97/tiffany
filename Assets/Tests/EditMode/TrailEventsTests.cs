using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class TrailEventsTests
    {
        private TrailEvents rules;
        [SetUp] public void SetUp() => rules = new TrailEvents();
        private TrailEvent Tick(float dt, bool trail = true, bool chase = false, float distance = 100f,
            bool tyler = true, bool flying = false, bool inChallenge = false) =>
            rules.Tick(dt, trail, chase, inChallenge, distance, tyler, flying);
        [Test]
        public void ClearingsStayQuietAndPauseTheTimer()
        {
            Assert.AreEqual(TrailEvent.None, Tick(100f, trail: false));
            Assert.AreEqual(TrailEvent.None, Tick(5f));
            Assert.AreEqual(TrailEvent.None, Tick(30f, trail: false));
            Assert.AreEqual(TrailEvent.None, Tick(4f));
            Assert.AreEqual(TrailEvent.PhantomSteps, Tick(1f));
        }
        [Test] public void IntervalsCycleAfterFirstTenSeconds()
        {
            Assert.AreEqual(TrailEvent.None, Tick(9f));
            Assert.AreEqual(TrailEvent.PhantomSteps, Tick(1f));
            foreach (float gap in new[] { 28f, 40f, 34f, 46f, 28f })
            {
                Assert.AreEqual(TrailEvent.None, Tick(gap - 1f));
                Assert.AreNotEqual(TrailEvent.None, Tick(1f));
            }
        }
        [TestCase(true, false, "PhantomSteps,BranchSnap,SkyCrossing,TylerGrowl,PhantomSteps")]
        [TestCase(false, false, "PhantomSteps,BranchSnap,SkyCrossing,PhantomSteps,BranchSnap")]
        [TestCase(true, true, "PhantomSteps,BranchSnap,TylerGrowl,PhantomSteps,BranchSnap")]
        [TestCase(false, true, "PhantomSteps,BranchSnap,PhantomSteps,BranchSnap,PhantomSteps")]
        public void RotationSkipsUnavailableEventsWithoutRepeating(bool tyler, bool flying, string expected)
        {
            foreach (string name in expected.Split(',')) Assert.AreEqual(name, Tick(46f, tyler: tyler, flying: flying).ToString());
        }
        [Test]
        public void ChaseBlocksUntilTwelveSecondsAfterItEnds()
        {
            Assert.AreEqual(TrailEvent.None, Tick(10f, chase: true));
            Assert.AreEqual(TrailEvent.None, Tick(4f, chase: true));
            Assert.AreEqual(TrailEvent.None, Tick(4f));
            Assert.AreEqual(TrailEvent.None, Tick(4f));
            Assert.AreEqual(TrailEvent.PhantomSteps, Tick(4f));
        }
        [TestCase(0, TrailEvent.PhantomSteps)]
        [TestCase(1, TrailEvent.BranchSnap)]
        [TestCase(2, TrailEvent.SkyCrossing)]
        [TestCase(3, TrailEvent.TylerGrowl)]
        public void ChallengeBlocksEveryEventAndRestartsChaseCooldown(int precedingEvents, TrailEvent next)
        {
            for (int i = 0; i < precedingEvents; i++) Assert.AreNotEqual(TrailEvent.None, Tick(46f));
            Assert.AreEqual(TrailEvent.None, Tick(60f, inChallenge: true));
            Assert.AreEqual(TrailEvent.None, Tick(TrailEvents.ChaseCooldown, inChallenge: true));
            Assert.AreEqual(TrailEvent.None, Tick(TrailEvents.ChaseCooldown / 3f));
            Assert.AreEqual(TrailEvent.None, Tick(TrailEvents.ChaseCooldown / 3f));
            Assert.AreEqual(TrailEvent.None, Tick(TrailEvents.ChaseCooldown / 3f - 0.5f));
            Assert.AreEqual(next, Tick(0.5f));
        }
        [Test]
        public void NearWitchRetriesAfterFourSecondsWithoutAdvancingEitherCycle()
        {
            Assert.AreEqual(TrailEvent.None, Tick(10f, distance: 19.9f));
            Assert.AreEqual(TrailEvent.None, Tick(3f, distance: 20f));
            Assert.AreEqual(TrailEvent.PhantomSteps, Tick(1f, distance: 20f));
            Assert.AreEqual(TrailEvent.None, Tick(27f));
            Assert.AreEqual(TrailEvent.BranchSnap, Tick(1f));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaDoesNotAdvanceTheTimer(float dt)
        {
            Assert.AreEqual(TrailEvent.None, Tick(dt));
            Assert.AreEqual(TrailEvent.None, Tick(9f));
            Assert.AreEqual(TrailEvent.PhantomSteps, Tick(1f));
        }
    }
}
