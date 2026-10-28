using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class PlayerFacingTests
    {
        [TestCase(0f, 2f, 1f)]
        [TestCase(0f, -2f, 0.7f)]
        [TestCase(2f, 0f, 0.7f)]
        [TestCase(2f, -2f, 0.7f)]
        [TestCase(2f, 2f, 0.912132f)]
        [TestCase(0f, 0f, 1f)]
        public void WalkingSpeedUsesNormalizedHorizontalDirections(float x, float z, float expected)
        {
            Assert.AreEqual(expected, PlayerFacing.SpeedFactor(new Vector3(x, 3f, z),
                new Vector3(0f, 5f, 4f), false, 0.7f), 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        public void RunningKeepsFullSpeedAtEveryAngle(float angle)
        {
            var move = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Assert.AreEqual(1f, PlayerFacing.SpeedFactor(move, Vector3.forward, true, 0.7f));
        }

        [TestCase(Gait.Slow, 0f, false)]
        [TestCase(Gait.Slow, 1f, false)]
        [TestCase(Gait.Walk, 0f, false)]
        [TestCase(Gait.Walk, 1f, false)]
        [TestCase(Gait.Run, 0f, false)]
        [TestCase(Gait.Run, 1f, true)]
        public void OnlyRunningWithHorizontalMovementFacesMovement(Gait gait, float z, bool expected)
        {
            Assert.AreEqual(expected, PlayerFacing.FacesMovement(gait, new Vector3(0f, 3f, z)));
        }

        [Test]
        public void ReturnUsesSlowTurnUntilAlignedThenStaysAtNormalSpeed()
        {
            var state = new PlayerFacing.TurnState();
            Assert.AreEqual(720f, state.Speed(false, 180f, 720f, 300f));
            Assert.AreEqual(720f, state.Speed(true, 180f, 720f, 300f));
            Assert.AreEqual(300f, state.Speed(false, 180f, 720f, 300f));
            Assert.AreEqual(300f, state.Speed(false, 2.01f, 720f, 300f));
            Assert.AreEqual(720f, state.Speed(false, 2f, 720f, 300f));
            Assert.AreEqual(720f, state.Speed(false, 90f, 720f, 300f));
        }

        [Test]
        public void RunningAgainCancelsReturnAndAllowsAnotherReturn()
        {
            var state = new PlayerFacing.TurnState();
            state.Speed(true, 180f, 720f, 300f);
            Assert.AreEqual(300f, state.Speed(false, 180f, 720f, 300f));
            Assert.AreEqual(720f, state.Speed(true, 180f, 720f, 300f));
            Assert.AreEqual(720f, state.Speed(true, 90f, 720f, 300f));
            Assert.AreEqual(300f, state.Speed(false, 90f, 720f, 300f));
        }
    }
}
