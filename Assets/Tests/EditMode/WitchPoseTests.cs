using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WitchPoseTests
    {
        [Test]
        public void ConstructorPreservesEveryProperty()
        {
            var pose = new WitchPose(true, true, true);

            Assert.IsTrue(pose.FigureVisible);
            Assert.IsTrue(pose.BallVisible);
            Assert.IsTrue(pose.LightOn);
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
        }

        [TestCase(WitchState.Stalking)]
        [TestCase(WitchState.Stunned)]
        [TestCase(WitchState.Caught)]
        [TestCase(WitchState.WindUp)]
        [TestCase(WitchState.Sprinting)]
        [TestCase(WitchState.Recovering)]
        public void GroundedPoseShowsFigureWithoutFire(WitchState state)
        {
            foreach (bool fireLit in new[] { false, true })
            {
                WitchPose pose = WitchPoses.For(state, fireLit);

                Assert.IsTrue(pose.FigureVisible);
                Assert.IsFalse(pose.BallVisible);
                Assert.IsFalse(pose.LightOn);
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
