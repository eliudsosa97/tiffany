using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class ChallengeRulesTests
    {
        [Test]
        public void DarknessNeedsBothCandlesAndNeverTrapsPlayerWhenTheyBurnOut()
        {
            var rules = new ChallengeRules(ChallengeKind.Candles);
            rules.Tick(-1f, true, 0, false, false);
            Assert.IsFalse(rules.GateOpen(0));
            Assert.IsFalse(rules.GateOpen(1));
            rules.Tick(-1f, true, 1, false, false);
            Assert.IsTrue(rules.GateOpen(0));
            Assert.IsFalse(rules.GateOpen(1));
            rules.Tick(-1f, true, 2, false, false);
            rules.Tick(1f, true, 0, false, false);
            rules.Tick(ChallengeRules.PassageLength, true, 0, false, false);
            Assert.IsTrue(rules.Completed);
            rules.ResetAttempt();
            Assert.IsTrue(rules.GateOpen(0) && rules.GateOpen(1));
        }

        [TestCase(ChallengeKind.Candles, 0, false, false)]
        [TestCase(ChallengeKind.Distract, 0, false, false)]
        [TestCase(ChallengeKind.Vanish, 0, false, false)]
        [TestCase(ChallengeKind.Hut, 0, true, true)]
        [TestCase(ChallengeKind.Hut, 3, false, true)]
        [TestCase(ChallengeKind.Hut, 3, true, false)]
        public void CannotCompleteWithoutRequiredPower(ChallengeKind kind, int candles, bool distracted, bool vanished)
        {
            var rules = new ChallengeRules(kind);
            rules.Tick(-1f, true, candles, distracted, vanished);
            rules.Tick(1f, true, candles, distracted, vanished);
            rules.Tick(ChallengeRules.PassageLength, true, candles, distracted, vanished);
            Assert.IsFalse(rules.Completed);
        }

        [TestCase(ChallengeKind.Candles)]
        [TestCase(ChallengeKind.Distract)]
        [TestCase(ChallengeKind.Vanish)]
        [TestCase(ChallengeKind.Hut)]
        public void CaptureResetsAnAttemptAndTheSamePowersCanSolveItAgain(ChallengeKind kind)
        {
            var rules = new ChallengeRules(kind);
            rules.Tick(-1f, true, 3, true, true);
            rules.Tick(1f, true, 3, true, true);
            Assert.IsTrue(rules.Crossing);
            rules.ResetAttempt();
            Assert.AreEqual(0, rules.LitMask);
            Assert.IsFalse(rules.Crossing || rules.Completed);
            rules.Tick(-1f, true, 3, true, true);
            rules.Tick(1f, true, 3, true, true);
            rules.Tick(ChallengeRules.PassageLength, true, 3, true, true);
            Assert.IsTrue(rules.Completed);
        }

        [Test]
        public void VanishMustLastForTheWholePassageAndCanBeRetried()
        {
            var rules = new ChallengeRules(ChallengeKind.Vanish);
            rules.Tick(-0.3f, true, 0, false, true);
            rules.Tick(1f, true, 0, false, true);
            rules.Tick(2f, true, 0, false, false);
            rules.Tick(ChallengeRules.PassageLength, true, 0, false, true);
            Assert.IsFalse(rules.Completed);
            rules.Tick(-0.3f, true, 0, false, true);
            rules.Tick(1f, true, 0, false, true);
            rules.Tick(ChallengeRules.PassageLength, true, 0, false, true);
            Assert.IsTrue(rules.Completed);
            Assert.Less((ChallengeRules.PassageLength + 0.6f) / PlayerMotion.WalkSpeed + 0.2f, Vanish.Duration);
        }

        [Test]
        public void TeleportingToTheExitOrLeavingTheLaneDoesNotProveACrossing()
        {
            var rules = new ChallengeRules(ChallengeKind.Hut);
            rules.Tick(ChallengeRules.PassageLength, true, 3, true, true);
            Assert.IsFalse(rules.Completed);
            rules.Tick(-1f, true, 3, true, true);
            rules.Tick(1f, true, 3, true, true);
            rules.Tick(2f, false, 3, true, true);
            rules.Tick(ChallengeRules.PassageLength, true, 3, true, true);
            Assert.IsFalse(rules.Completed);
        }

        [Test]
        public void LureRetryWaitsForTheSoundToBeProcessedAndForPlayerToReturn()
        {
            Assert.IsFalse(ChallengeRules.CanRestoreLure(true, true, false, 0f));
            Assert.IsFalse(ChallengeRules.CanRestoreLure(false, true, false, 8f));
            Assert.IsFalse(ChallengeRules.CanRestoreLure(true, true, true, 8f));
            Assert.IsTrue(ChallengeRules.CanRestoreLure(true, true, false, 0.5f));
        }
    }
}
