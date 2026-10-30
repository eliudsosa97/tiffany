using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class FootstepCadenceTests
    {
        [TestCase(Gait.Slow, 0.55f)]
        [TestCase(Gait.Walk, 0.75f)]
        [TestCase(Gait.Run, 1f)]
        public void DistanceTriggersAtTheRequestedStrideAndKeepsRemainder(Gait gait, float stride)
        {
            var cadence = new FootstepCadence();
            Assert.AreEqual(0, cadence.Advance(stride - 0.01f, gait, 0));
            Assert.AreEqual(1, cadence.Advance(0.02f, gait, 0));
            Assert.AreEqual(0, cadence.Advance(stride - 0.02f, gait, 0));
            Assert.AreEqual(1, cadence.Advance(0.02f, gait, 0));
        }

        [TestCase(Gait.Slow, 1.4f, 0.55f)]
        [TestCase(Gait.Walk, 4f, 0.75f)]
        [TestCase(Gait.Run, 5.5f, 1f)]
        public void CadenceDependsOnTravelAcrossFrameRates(Gait gait, float speed, float stride)
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var cadence = new FootstepCadence();
                int count = 0;
                for (int frame = 0; frame < fps * 5 / 2; frame++)
                    count += cadence.Advance(speed / fps, gait, 0);
                Assert.AreEqual((int)(speed * 2.5f / stride), count);
            }
        }

        [Test]
        public void LargeMovementPublishesAllCrossedStrides()
        {
            var cadence = new FootstepCadence();
            Assert.AreEqual(3, cadence.Advance(2.5f, Gait.Walk, 0));
            Assert.AreEqual(1, cadence.Advance(0.5f, Gait.Walk, 0));
        }

        [Test]
        public void StoppingPreservesPartialStrideWithoutPublishingSteps()
        {
            var cadence = new FootstepCadence();
            Assert.AreEqual(0, cadence.Advance(0.5f, Gait.Walk, 0));
            for (int frame = 0; frame < 600; frame++) Assert.AreEqual(0, cadence.Advance(0f, Gait.Walk, 0));
            Assert.AreEqual(1, cadence.Advance(0.25f, Gait.Walk, 0));
        }

        [Test]
        public void ContactsTakePriorityAndResetTheDistanceFallback()
        {
            var cadence = new FootstepCadence();
            Assert.AreEqual(1, cadence.Advance(0.75f, Gait.Walk, 1));
            Assert.AreEqual(2, cadence.Advance(0f, Gait.Walk, 2));
            Assert.AreEqual(0, cadence.Advance(0.74f, Gait.Walk, 0));
            Assert.AreEqual(1, cadence.Advance(0.02f, Gait.Walk, 0));
        }

        [Test]
        public void DistanceFallbackSuppressesTheFollowingCloseContact()
        {
            var cadence = new FootstepCadence();
            Assert.AreEqual(1, cadence.Advance(0.75f, Gait.Walk, 0));
            Assert.AreEqual(0, cadence.Advance(0f, Gait.Walk, 1));
            Assert.AreEqual(0, cadence.Advance(0.1f, Gait.Walk, 1));
            Assert.AreEqual(1, cadence.Advance(0.1f, Gait.Walk, 1));
        }
    }
}
