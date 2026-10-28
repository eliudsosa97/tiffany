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
