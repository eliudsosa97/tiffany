using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class PlayerFacingTests
    {
        [TestCase(0f, 90f, 30)]
        [TestCase(0f, -90f, 60)]
        [TestCase(350f, 10f, 144)]
        [TestCase(10f, 350f, 60)]
        [TestCase(0f, 180f, 60)]
        public void DampedTurnNeverPassesTargetAndSettlesQuickly(float yaw, float target, int fps)
        {
            var state = new PlayerFacing.TurnState();
            float sign = Mathf.Sign(Mathf.DeltaAngle(yaw, target));
            float remaining = Mathf.Abs(Mathf.DeltaAngle(yaw, target));
            for (int i = 0; i < fps / 2; i++)
            {
                float next = state.Advance(yaw, target, false, 720f, 300f, 1f / fps);
                float delta = Mathf.DeltaAngle(yaw, next);
                Assert.GreaterOrEqual(delta * sign, -0.0001f);
                Assert.LessOrEqual(Mathf.Abs(delta), remaining + 0.0001f);
                Assert.LessOrEqual(Mathf.Abs(state.AngularSpeed), 720f);
                yaw = next;
                remaining = Mathf.Abs(Mathf.DeltaAngle(yaw, target));
            }
            Assert.Less(remaining, 0.5f);
        }

        [Test]
        public void TurnAcceleratesThenBrakesAndReportsActualAngularSpeed()
        {
            var state = new PlayerFacing.TurnState();
            const float dt = 1f / 60f;
            float yaw = state.Advance(0f, 90f, false, 720f, 300f, dt);
            float first = state.AngularSpeed;
            float next = state.Advance(yaw, 90f, false, 720f, 300f, dt);
            Assert.Greater(state.AngularSpeed, first);
            Assert.AreEqual(Mathf.DeltaAngle(yaw, next) / dt, state.AngularSpeed, 1e-4f);
            for (int i = 0; i < 60; i++) next = state.Advance(next, 90f, false, 720f, 300f, dt);
            Assert.Less(Mathf.Abs(state.AngularSpeed), 1f);
        }

        [Test]
        public void ReversingTargetDoesNotCrossItAndClearingAimStopsRotation()
        {
            var state = new PlayerFacing.TurnState();
            float yaw = state.Advance(0f, 90f, false, 720f, 300f, 0.05f);
            for (int i = 0; i < 60; i++)
            {
                yaw = state.Advance(yaw, -30f, false, 720f, 300f, 1f / 60f);
                Assert.GreaterOrEqual(yaw, -30f);
            }
            Assert.AreEqual(yaw, state.Advance(yaw, yaw, false, 720f, 300f, 0.1f));
            Assert.AreEqual(0f, state.AngularSpeed);
        }

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
