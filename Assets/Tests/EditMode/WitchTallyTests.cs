using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WitchTallyTests
    {
        [TestCase(WitchState.Flying, false, WitchTallyEvent.None)]
        [TestCase(WitchState.Suspicious, false, WitchTallyEvent.Suspicion)]
        [TestCase(WitchState.Stalking, true, WitchTallyEvent.Chase)]
        [TestCase(WitchState.Sprinting, false, WitchTallyEvent.Sprint)]
        [TestCase(WitchState.Caught, false, WitchTallyEvent.Catch)]
        public void FirstObservationUsesFlyingAndNoChaseAsBaseline(WitchState state, bool chasing, WitchTallyEvent expected) =>
            Assert.AreEqual(expected, new WitchTally(2).Observe(0, state, chasing));

        [Test]
        public void SuspicionCountsOnEntryButNotWhileRemainingSuspicious()
        {
            var tally = new WitchTally(2);
            Assert.AreEqual(WitchTallyEvent.Suspicion, tally.Observe(0, WitchState.Suspicious, false));
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(0, WitchState.Suspicious, false));
            Assert.AreEqual(1, tally.Suspicions(0));
        }

        [Test]
        public void ReenteringSuspicionCountsAgain()
        {
            var tally = new WitchTally(2);
            tally.Observe(0, WitchState.Suspicious, false);
            tally.Observe(0, WitchState.Stalking, false);
            tally.Observe(0, WitchState.Suspicious, false);
            Assert.AreEqual(2, tally.Suspicions(0));
        }

        [Test]
        public void ChaseCountsOnlyOnRisingEdge()
        {
            var tally = new WitchTally(2);
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(0, WitchState.Stalking, false));
            Assert.AreEqual(WitchTallyEvent.Chase, tally.Observe(0, WitchState.Stalking, true));
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(0, WitchState.Stalking, true));
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(0, WitchState.Stalking, false));
            Assert.AreEqual(WitchTallyEvent.Chase, tally.Observe(0, WitchState.Stalking, true));
            Assert.AreEqual(2, tally.Chases(0));
        }

        [TestCase(WitchState.Sprinting, WitchTallyEvent.Sprint)]
        [TestCase(WitchState.Caught, WitchTallyEvent.Catch)]
        public void SprintAndCatchCountOnlyOnEntry(WitchState state, WitchTallyEvent expected)
        {
            var tally = new WitchTally(2);
            Assert.AreEqual(expected, tally.Observe(0, state, false));
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(0, state, false));
            Assert.AreEqual(state == WitchState.Sprinting ? 1 : 0, tally.Sprints(0));
            Assert.AreEqual(state == WitchState.Caught ? 1 : 0, tally.Catches(0));
        }

        [TestCase(WitchState.Suspicious, WitchTallyEvent.Suspicion)]
        [TestCase(WitchState.Sprinting, WitchTallyEvent.Sprint)]
        [TestCase(WitchState.Caught, WitchTallyEvent.Catch)]
        public void SimultaneousStateAndChaseEventsBothCount(WitchState state, WitchTallyEvent expected)
        {
            var tally = new WitchTally(2);
            Assert.AreEqual(expected | WitchTallyEvent.Chase, tally.Observe(0, state, true));
            Assert.AreEqual(1, tally.TotalChases);
            Assert.AreEqual(2, tally.TotalSuspicions + tally.TotalChases + tally.TotalSprints + tally.TotalCatches);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void EventsBelongOnlyToTheIndicatedClearing(int clearing)
        {
            var tally = new WitchTally(2);
            ObserveAll(tally, clearing);
            Assert.AreEqual(WitchTallyEvent.None, tally.Observe(1 - clearing, WitchState.Caught, true));
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, Counts(tally, clearing));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, Counts(tally, 1 - clearing));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, Counts(tally, -1));
        }

        [TestCase(2, -1)]
        [TestCase(2, -8)]
        [TestCase(2, 2)]
        [TestCase(0, 0)]
        public void InvalidClearingsUseTheUnknownBucket(int clearingCount, int clearing)
        {
            var tally = new WitchTally(clearingCount);
            ObserveAll(tally, clearing);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, Counts(tally, -1));
            CollectionAssert.AreEqual(Counts(tally, -1), Counts(tally, clearing));
        }

        [Test]
        public void TotalsIncludeAllClearingsAndTheUnknownBucket()
        {
            var tally = new WitchTally(2);
            ObserveAll(tally, 0);
            ObserveAll(tally, 1);
            ObserveAll(tally, -1);
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3 },
                new[] { tally.TotalSuspicions, tally.TotalChases, tally.TotalSprints, tally.TotalCatches });
        }

        [Test]
        public void SummaryListsRouteOrderThenUnknownAndTotalsWithTheirCounts()
        {
            var tally = new WitchTally(2);
            tally.Observe(1, WitchState.Suspicious, false);
            tally.Observe(0, WitchState.Sprinting, true);
            tally.Observe(-1, WitchState.Caught, true);
            CollectionAssert.AreEqual(new[]
            {
                "Pino: sospechas 0, cazas 1, embestidas 1, atrapadas 0",
                "Jacal: sospechas 1, cazas 0, embestidas 0, atrapadas 0",
                "sin claro: sospechas 0, cazas 0, embestidas 0, atrapadas 1",
                "totales: sospechas 1, cazas 1, embestidas 1, atrapadas 1"
            }, tally.SummaryLines(new[] { "Pino", "Jacal" }));
        }

        [Test]
        public void EmptySummaryKeepsRouteClearingsAndOmitsUnknown() =>
            CollectionAssert.AreEqual(new[]
            {
                "Pino: sospechas 0, cazas 0, embestidas 0, atrapadas 0",
                "totales: sospechas 0, cazas 0, embestidas 0, atrapadas 0"
            }, new WitchTally(1).SummaryLines(new[] { "Pino" }));

        static void ObserveAll(WitchTally tally, int clearing)
        {
            tally.Observe(clearing, WitchState.Suspicious, false);
            tally.Observe(clearing, WitchState.Stalking, true);
            tally.Observe(clearing, WitchState.Sprinting, true);
            tally.Observe(clearing, WitchState.Caught, true);
        }

        static int[] Counts(WitchTally tally, int clearing) =>
            new[] { tally.Suspicions(clearing), tally.Chases(clearing), tally.Sprints(clearing), tally.Catches(clearing) };
    }
}
