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
            Assert.AreEqual(expected, TiffanyAnimation.StateFor(caught, vanishing, hidden, speed, angularSpeed));
        }

        [Test]
        public void SlowAngularSpeedStaysIdleAndNearlyStillSpeedTurns()
        {
            float below = TiffanyAnimation.TurnSpeed * 0.9f;
            float above = TiffanyAnimation.TurnSpeed * 1.1f;
            Assert.AreEqual(TiffanyAnimation.Idle, TiffanyAnimation.StateFor(false, false, false, 0f, below));
            Assert.AreEqual(TiffanyAnimation.Idle, TiffanyAnimation.StateFor(false, false, false, 0f, -below));
            Assert.AreEqual(TiffanyAnimation.TurnRight, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed, above));
            Assert.AreEqual(TiffanyAnimation.TurnLeft, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed, -above));
            Assert.AreEqual(TiffanyAnimation.Walk, TiffanyAnimation.StateFor(false, false, false, TiffanyAnimation.MoveSpeed * 1.5f, 0f));
        }

        [Test]
        public void EveryStateHasAPositiveBlendTime()
        {
            foreach (string state in new[] { TiffanyAnimation.Idle, TiffanyAnimation.Walk, TiffanyAnimation.TurnLeft, TiffanyAnimation.TurnRight,
                         TiffanyAnimation.Hide, TiffanyAnimation.Caught, TiffanyAnimation.Vanish, TiffanyAnimation.Empty,
                         TiffanyAnimation.CarryLight, TiffanyAnimation.Flash })
                Assert.That(TiffanyAnimation.BlendFor(state), Is.InRange(0.05f, 0.5f), state);
            Assert.Throws<ArgumentOutOfRangeException>(() => TiffanyAnimation.BlendFor("Dance"));
        }
    }
}
