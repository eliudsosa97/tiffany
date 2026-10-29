using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class PlayerMotionTests
    {
        [TestCase(Gait.Slow, 1.1f)]
        [TestCase(Gait.Walk, 2.2f)]
        [TestCase(Gait.Run, 4f)]
        public void SpeedMatchesDesign(Gait gait, float expected)
        {
            Assert.AreEqual(expected, PlayerMotion.Speed(gait), 1e-4f);
        }

        [TestCase(0f, Gait.Slow)]
        [TestCase(0.55f, Gait.Slow)]
        [TestCase(1.1f, Gait.Slow)]
        [TestCase(1.64f, Gait.Slow)]
        [TestCase(1.66f, Gait.Walk)]
        [TestCase(2.2f, Gait.Walk)]
        [TestCase(3.09f, Gait.Walk)]
        [TestCase(3.11f, Gait.Run)]
        [TestCase(4f, Gait.Run)]
        [TestCase(PlayerMotion.SlowWalkThreshold, Gait.Walk)]
        [TestCase(PlayerMotion.WalkRunThreshold, Gait.Run)]
        public void GaitForSpeedUsesSpeedMidpoints(float speed, Gait expected)
        {
            Assert.AreEqual(expected, PlayerMotion.GaitForSpeed(speed));
        }

        [TestCase(0f, Gait.Slow)]
        [TestCase(1f, Gait.Slow)]
        [TestCase(PlayerMotion.SlowWalkThreshold, Gait.Walk)]
        [TestCase(2.2f, Gait.Walk)]
        [TestCase(3.5f, Gait.Run)]
        public void NoiseGaitUsesMeasuredSpeedWhileAcceleratingAndBraking(float speed, Gait expected)
        {
            Assert.AreEqual(expected, PlayerMotion.NoiseGait(speed));
        }

        [TestCase(Gait.Walk, 0.5f, 2.2f)]
        [TestCase(Gait.Run, 0.5f, 2.2f)]
        [TestCase(Gait.Run, 1f, 4f)]
        [TestCase(Gait.Walk, 1f, 2.2f)]
        [TestCase(Gait.Slow, 1f, 1.1f)]
        [TestCase(Gait.Walk, 0.25f, 1.1f)]
        [TestCase(Gait.Run, 0f, 0f)]
        public void AnalogSpeedWalksAtHalfStickAndRequiresRunForFullSpeed(Gait gait, float amount, float expected)
        {
            Assert.AreEqual(expected, PlayerMotion.TargetSpeed(amount, gait), 1e-5f);
        }

        [Test]
        public void ReleasingStopsBetween150And250MillisecondsWithoutRebound(
            [Values(30, 60, 144)] int fps, [Values(Gait.Slow, Gait.Walk, Gait.Run)] Gait gait)
        {
            var state = new PlayerMotion.VelocityState();
            float dt = 1f / fps;
            for (int i = 0; i < fps; i++) state.Advance(Vector3.forward * PlayerMotion.Speed(gait), dt);
            float stoppedAt = 0f;
            float previous = state.Velocity.z;
            for (int frame = 1; frame <= fps; frame++)
            {
                var velocity = state.Advance(Vector3.zero, dt);
                Assert.That(velocity.z, Is.InRange(0f, previous));
                previous = velocity.z;
                if (velocity == Vector3.zero && stoppedAt == 0f) stoppedAt = frame * dt;
            }
            Assert.That(stoppedAt, Is.InRange(0.15f, 0.25f));
        }

        [Test]
        public void StartAcceleratesGraduallyAndGaitChangesNeverOvershoot()
        {
            var state = new PlayerMotion.VelocityState();
            const float dt = 1f / 60f;
            float first = state.Advance(Vector3.forward * PlayerMotion.WalkSpeed, dt).z;
            float second = state.Advance(Vector3.forward * PlayerMotion.WalkSpeed, dt).z - first;
            Assert.That(first, Is.InRange(0.01f, 0.25f));
            Assert.Greater(second, first);
            foreach (float target in new[] { PlayerMotion.WalkSpeed, PlayerMotion.RunSpeed, PlayerMotion.SlowSpeed })
            {
                float from = state.Velocity.z;
                for (int i = 0; i < 60; i++)
                    Assert.That(state.Advance(Vector3.forward * target, dt).z,
                        Is.InRange(Mathf.Min(from, target), Mathf.Max(from, target)));
                Assert.AreEqual(target, state.Velocity.z, 0.001f);
            }
        }

        [Test]
        public void PausedMotionAndResetHaveNoResidualVelocity()
        {
            var state = new PlayerMotion.VelocityState();
            var velocity = state.Advance(Vector3.forward, 0.1f);
            Assert.AreEqual(velocity, state.Advance(Vector3.zero, 0f));
            state = default;
            Assert.AreEqual(Vector3.zero, state.Advance(Vector3.zero, 0.1f));
        }

        [TestCase(Gait.Slow)]
        [TestCase(Gait.Walk)]
        [TestCase(Gait.Run)]
        public void DesignSpeedResolvesToItsOwnGait(Gait gait)
        {
            Assert.AreEqual(gait, PlayerMotion.GaitForSpeed(PlayerMotion.Speed(gait)));
        }

        [TestCase(false, false, Gait.Walk)]
        [TestCase(true, false, Gait.Slow)]
        [TestCase(false, true, Gait.Run)]
        [TestCase(true, true, Gait.Run)]
        public void ResolveGaitPrioritizesRunning(bool slow, bool run, Gait expected)
        {
            Assert.AreEqual(expected, PlayerMotion.ResolveGait(slow, run));
        }

        [TestCase(0f, 0f, 1f, 0f, 1f)]
        [TestCase(0f, 1f, 0f, 1f, 0f)]
        [TestCase(90f, 0f, 1f, 1f, 0f)]
        [TestCase(90f, 1f, 0f, 0f, -1f)]
        [TestCase(45f, 0f, 1f, 0.7071068f, 0.7071068f)]
        [TestCase(45f, 1f, 0f, 0.7071068f, -0.7071068f)]
        public void CameraRelativeRotatesAroundYaw(float yaw, float x, float y, float expectedX, float expectedZ)
        {
            var result = PlayerMotion.CameraRelative(new Vector2(x, y), yaw);

            Assert.AreEqual(expectedX, result.x, 1e-4f);
            Assert.AreEqual(0f, result.y, 1e-4f);
            Assert.AreEqual(expectedZ, result.z, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(45f)]
        public void CameraRelativePreservesPartialStickMagnitude(float yaw)
        {
            var input = new Vector2(0.3f, -0.4f);

            Assert.AreEqual(input.magnitude, PlayerMotion.CameraRelative(input, yaw).magnitude, 1e-4f);
        }
    }
}
