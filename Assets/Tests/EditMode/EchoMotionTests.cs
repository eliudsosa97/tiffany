using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class EchoMotionTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.02f, 1f)]
        [TestCase(0.06f, 3f)]
        public void ConvergesToDisplacementPerSecond(float displacement, float expected)
        {
            float speed = 0f;
            for (int i = 0; i < 100; i++) speed = EchoMotion.Speed(displacement, 0.02f, speed);
            Assert.AreEqual(expected, speed, 0.0001f);
        }

        [Test]
        public void SmoothsAccelerationAndStoppingWithoutOvershoot()
        {
            float speed = EchoMotion.Speed(0.04f, 0.02f, 0f);
            Assert.That(speed, Is.GreaterThan(0f).And.LessThan(2f));
            float slowing = EchoMotion.Speed(0f, 0.02f, speed);
            Assert.That(slowing, Is.GreaterThan(0f).And.LessThan(speed));
            for (int i = 0; i < 50; i++) slowing = EchoMotion.Speed(0f, 0.02f, slowing);
            Assert.Less(slowing, TylerMotion.StillSpeed);
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(10f, 0f, 2f)]
        [TestCase(10f, -1f, 2f)]
        public void NonPositiveTimePreservesFiniteSpeed(float displacement, float dt, float previous)
        {
            float speed = EchoMotion.Speed(displacement, dt, previous);
            Assert.IsFalse(float.IsNaN(speed));
            Assert.IsFalse(float.IsInfinity(speed));
            Assert.AreEqual(previous, speed);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SmoothingIsIndependentOfFrameRate(int frames)
        {
            float speed = 0f;
            float dt = 0.2f / frames;
            for (int i = 0; i < frames; i++) speed = EchoMotion.Speed(2f * dt, dt, speed);
            Assert.AreEqual(EchoMotion.Speed(0.4f, 0.2f, 0f), speed, 0.0001f);
        }
    }
}
