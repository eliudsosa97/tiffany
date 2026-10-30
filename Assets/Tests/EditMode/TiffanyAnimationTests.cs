using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class TiffanyAnimationTests
    {
        [TestCase(true, true, true, 3f, 200f, TiffanyAnimation.Caught)]
        [TestCase(false, true, true, 3f, 200f, TiffanyAnimation.Vanish)]
        [TestCase(false, false, true, 3f, 200f, TiffanyAnimation.Hide)]
        [TestCase(false, false, false, 3f, 200f, TiffanyAnimation.Walk)]
        [TestCase(false, false, false, 0.5f, -200f, TiffanyAnimation.Walk)]
        [TestCase(false, false, false, 0f, 200f, TiffanyAnimation.TurnRight)]
        [TestCase(false, false, false, 0.05f, -90f, TiffanyAnimation.TurnLeft)]
        [TestCase(false, false, false, 0f, 0f, TiffanyAnimation.Idle)]
        public void StateFollowsThePriority(bool caught, bool vanishing, bool hidden, float speed, float angularSpeed, string expected)
        {
            Assert.AreEqual(expected, TiffanyAnimation.StateFor(caught, vanishing, hidden, speed, angularSpeed, false));
            string reverse = expected == TiffanyAnimation.Walk ? TiffanyAnimation.WalkBack : expected;
            Assert.AreEqual(reverse, TiffanyAnimation.StateFor(caught, vanishing, hidden, speed, angularSpeed, true));
        }

        [Test]
        public void SlowAngularSpeedStaysIdleAndNearlyStillSpeedTurns()
        {
            float below = TiffanyAnimation.TurnSpeed * 0.9f;
            float above = TiffanyAnimation.TurnSpeed * 1.1f;
            Assert.AreEqual(TiffanyAnimation.Idle, TiffanyAnimation.StateFor(false, false, false, 0f, below, false));
            Assert.AreEqual(TiffanyAnimation.Idle, TiffanyAnimation.StateFor(false, false, false, 0f, -below, false));
            Assert.AreEqual(TiffanyAnimation.TurnRight, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed, above, false));
            Assert.AreEqual(TiffanyAnimation.TurnLeft, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed, -above, false));
            Assert.AreEqual(TiffanyAnimation.Walk, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed * 1.5f, 0f, false));
        }

        [Test]
        public void EveryStateHasAPositiveBlendTime()
        {
            foreach (string state in new[] { TiffanyAnimation.Idle, TiffanyAnimation.Walk, TiffanyAnimation.WalkBack, TiffanyAnimation.TurnLeft, TiffanyAnimation.TurnRight,
                         TiffanyAnimation.Hide, TiffanyAnimation.Caught, TiffanyAnimation.Vanish, TiffanyAnimation.Empty,
                         TiffanyAnimation.CarryLight, TiffanyAnimation.Flash })
                Assert.That(TiffanyAnimation.BlendFor(state), Is.InRange(0.05f, 0.5f), state);
            Assert.Throws<ArgumentOutOfRangeException>(() => TiffanyAnimation.BlendFor("Dance"));
            Assert.AreEqual(TiffanyAnimation.WalkBlend, TiffanyAnimation.BlendFor(TiffanyAnimation.WalkBack));
        }

        [TestCase(-180f, 90f, 2f)]
        [TestCase(45f, 90f, 0.5f)]
        [TestCase(180f, 60f, 3f)]
        public void TurnPlaybackUsesAngularSpeedAndTheAuthoredRate(float angular, float authored, float expected)
        {
            Assert.AreEqual(expected, TiffanyAnimation.TurnRate(angular, authored));
        }

        [Test]
        public void FlashCompletesItsClipThenReturnsToLightPose()
        {
            var arms = new TiffanyAnimation.ArmsState();
            Assert.AreEqual(TiffanyAnimation.CarryLight, arms.Advance(false, true, 0f, 0.7f, 0.01f));
            Assert.AreEqual(TiffanyAnimation.Flash, arms.Advance(false, true, 0.2f, 0.7f, 0.01f));
            Assert.IsTrue(arms.RestartFlash);
            Assert.AreEqual(TiffanyAnimation.Flash, arms.Advance(false, true, 0f, 0.7f, 0.3f));
            Assert.IsFalse(arms.RestartFlash);
            Assert.AreEqual(TiffanyAnimation.CarryLight, arms.Advance(false, true, 0f, 0.7f, 0.41f));
            Assert.AreEqual(TiffanyAnimation.Empty, arms.Advance(false, false, 0f, 0.7f, 0.01f));
        }

        [Test]
        public void NewFlashRestartsButSpecialPosesSuppressArmsAndClearPendingFlash()
        {
            var arms = new TiffanyAnimation.ArmsState();
            arms.Advance(false, true, 0.2f, 0.7f, 0.01f);
            arms.Advance(false, true, 0.1f, 0.7f, 0.1f);
            arms.Advance(false, true, 0.2f, 0.7f, 0.01f);
            Assert.IsTrue(arms.RestartFlash);
            Assert.AreEqual(TiffanyAnimation.Empty, arms.Advance(true, true, 0.1f, 0.7f, 0.01f));
            Assert.AreEqual(TiffanyAnimation.CarryLight, arms.Advance(false, true, 0f, 0.7f, 0.01f));
        }
    }
}
