using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchPoseTests
    {
        [Test]
        public void ConstructorPreservesEveryProperty()
        {
            var pose = new WitchPose(true, true, true, new Vector3(2f, 3f, 4f), 12f);

            Assert.IsTrue(pose.FigureVisible);
            Assert.IsTrue(pose.BallVisible);
            Assert.IsTrue(pose.LightOn);
            Assert.AreEqual(new Vector3(2f, 3f, 4f), pose.FigureScale);
            Assert.AreEqual(12f, pose.FigureLean);
        }

        [TestCase(WitchState.Flying, false, false)]
        [TestCase(WitchState.Flying, true, true)]
        [TestCase(WitchState.Landing, false, false)]
        [TestCase(WitchState.Landing, true, false)]
        [TestCase(WitchState.TakingOff, false, true)]
        [TestCase(WitchState.TakingOff, true, true)]
        public void AirbornePoseKeepsBallVisibleAndSelectsLight(WitchState state, bool fireLit, bool lightOn)
        {
            WitchPose pose = WitchPoses.For(state, fireLit);

            Assert.IsFalse(pose.FigureVisible);
            Assert.IsTrue(pose.BallVisible);
            Assert.AreEqual(lightOn, pose.LightOn);
            Assert.AreEqual(Vector3.one, pose.FigureScale);
            Assert.AreEqual(0f, pose.FigureLean);
        }

        [TestCase(WitchState.Stalking, 1f, 1f, 0f)]
        [TestCase(WitchState.Stunned, 1f, 1f, 0f)]
        [TestCase(WitchState.Caught, 1f, 1f, 0f)]
        [TestCase(WitchState.WindUp, 1.15f, 0.75f, 0f)]
        [TestCase(WitchState.Sprinting, 1f, 1f, 30f)]
        [TestCase(WitchState.Recovering, 1f, 1f, 15f)]
        public void GroundedPoseShowsFigureWithoutFire(WitchState state, float width, float height, float lean)
        {
            foreach (bool fireLit in new[] { false, true })
            {
                WitchPose pose = WitchPoses.For(state, fireLit);

                Assert.IsTrue(pose.FigureVisible);
                Assert.IsFalse(pose.BallVisible);
                Assert.IsFalse(pose.LightOn);
                Assert.AreEqual(new Vector3(width, height, width), pose.FigureScale);
                Assert.AreEqual(lean, pose.FigureLean);
            }
        }

        [TestCase(150f, -10f, 10f, 0f)]
        [TestCase(150f, 0f, 10f, 0f)]
        [TestCase(150f, 1f, 10f, 1.5f)]
        [TestCase(150f, 5f, 10f, 37.5f)]
        [TestCase(150f, 10f, 10f, 150f)]
        [TestCase(150f, 20f, 10f, 150f)]
        [TestCase(150f, 5f, 0f, 0f)]
        [TestCase(150f, 5f, -10f, 0f)]
        [TestCase(150f, 0f, 0f, 0f)]
        [TestCase(0f, 10f, 10f, 0f)]
        public void FireIntensityFallsQuadraticallyAndClampsAtGroundAndFullHeight(
            float baseIntensity, float height, float fullHeight, float expected)
        {
            Assert.AreEqual(expected, WitchPoses.FireIntensity(baseIntensity, height, fullHeight), 1e-4f);
        }
    }
}
