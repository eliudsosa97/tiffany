using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class RouteGuidanceTests
    {
        RouteGuidance guidance;

        [SetUp]
        public void SetUp()
        {
            guidance = new RouteGuidance();
            guidance.Tick(0f, 0f, 0, 100f, true);
        }

        [TestCase(10f, false)]
        [TestCase(10.01f, true)]
        [TestCase(15f, true)]
        public void OnlyRemindsBeyondTenMetresFromTheRoute(float distance, bool expected)
        {
            Assert.That(guidance.Tick(1f, distance, 0, 100f, true), Is.EqualTo(expected));
            Assert.That(guidance.IsVisible, Is.EqualTo(expected));
        }

        [Test]
        public void RemindsAfterTwentySecondsWithoutApproachingTheNextClearing()
        {
            Assert.That(guidance.Tick(19f, 0f, 0, 100f, true), Is.False);
            Assert.That(guidance.Tick(1f, 0f, 0, 100f, true), Is.True);
        }

        [Test]
        public void ApproachingTheNextClearingKeepsResettingTheStallTimer()
        {
            for (int step = 1; step <= 8; step++)
                Assert.That(guidance.Tick(5f, 0f, 0, 100f - step, true), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
        }

        [Test]
        public void MovingAwayOrOscillatingDoesNotCountAsProgress()
        {
            Assert.That(guidance.Tick(5f, 0f, 0, 102f, true), Is.False);
            Assert.That(guidance.Tick(5f, 0f, 0, 100f, true), Is.False);
            Assert.That(guidance.Tick(5f, 0f, 0, 99.8f, true), Is.False);
            Assert.That(guidance.Tick(5f, 0f, 0, 100f, true), Is.True);
        }

        [Test]
        public void CooldownPreventsRepeatingBeforeThirtySeconds()
        {
            Assert.That(guidance.Tick(0f, 11f, 0, 100f, true), Is.True);
            Assert.That(guidance.Tick(29f, 11f, 0, 100f, true), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
            Assert.That(guidance.Tick(1f, 11f, 0, 100f, true), Is.True);
        }

        [Test]
        public void ReminderExpiresAfterFourSeconds()
        {
            guidance.Tick(0f, 11f, 0, 100f, true);
            guidance.Tick(RouteGuidance.NoticeSeconds - 1f, 11f, 0, 100f, true);
            Assert.That(guidance.IsVisible, Is.True);
            guidance.Tick(1f, 11f, 0, 100f, true);
            Assert.That(guidance.IsVisible, Is.False);
        }

        [Test]
        public void ReachingAClearingResetsTheTimerWithoutShowingAReminder()
        {
            guidance.Tick(19f, 0f, 0, 100f, true);
            Assert.That(guidance.Tick(1f, 15f, 1, 80f, true), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
            Assert.That(guidance.Tick(19f, 0f, 1, 80f, true), Is.False);
            Assert.That(guidance.Tick(1f, 0f, 1, 80f, true), Is.True);
        }

        [Test]
        public void ClearingProgressHidesAnExistingReminderAndPreservesTheCooldown()
        {
            guidance.Tick(0f, 11f, 0, 100f, true);
            Assert.That(guidance.Tick(1f, 11f, 1, 80f, true), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
            Assert.That(guidance.Tick(28f, 11f, 1, 80f, true), Is.False);
            Assert.That(guidance.Tick(1f, 11f, 1, 80f, true), Is.True);
        }

        [Test]
        public void ApproachingTheNextClearingHidesAnExistingReminder()
        {
            guidance.Tick(20f, 0f, 0, 100f, true);
            Assert.That(guidance.IsVisible, Is.True);
            Assert.That(guidance.Tick(1f, 0f, 0, 99f, true), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
        }

        [TestCase(InterfaceScreen.Title, false, false, false)]
        [TestCase(InterfaceScreen.Story, false, false, false)]
        [TestCase(InterfaceScreen.Paused, false, false, false)]
        [TestCase(InterfaceScreen.Notebook, false, false, false)]
        [TestCase(InterfaceScreen.Ending, false, false, false)]
        [TestCase(InterfaceScreen.Playing, true, false, false)]
        [TestCase(InterfaceScreen.Playing, false, true, false)]
        [TestCase(InterfaceScreen.Playing, false, false, true)]
        public void SuppressedContextsHideTheReminderAndDoNotAccumulateStalledTime(
            InterfaceScreen screen, bool echo, bool chasing, bool inChallenge)
        {
            guidance.Tick(0f, 11f, 0, 100f, true);
            bool allowed = RouteGuidance.CanRemind(screen, echo, chasing, inChallenge);
            Assert.That(allowed, Is.False);
            Assert.That(guidance.Tick(60f, 11f, 0, 100f, allowed), Is.False);
            Assert.That(guidance.IsVisible, Is.False);
            Assert.That(guidance.Tick(19f, 0f, 0, 100f, true), Is.False);
            Assert.That(guidance.Tick(1f, 0f, 0, 100f, true), Is.True);
        }

        [Test]
        public void ExplorationAllowsReminders() =>
            Assert.That(RouteGuidance.CanRemind(InterfaceScreen.Playing, false, false, false), Is.True);

        [TestCase(0f)]
        [TestCase(-20f)]
        public void NonPositiveTimeDoesNotAccumulateStalledTime(float dt)
        {
            guidance.Tick(dt, 0f, 0, 100f, true);
            Assert.That(guidance.Tick(19f, 0f, 0, 100f, true), Is.False);
            Assert.That(guidance.Tick(1f, 0f, 0, 100f, true), Is.True);
        }
    }
}
