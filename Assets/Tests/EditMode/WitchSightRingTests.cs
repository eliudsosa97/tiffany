using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class WitchSightRingTests
    {
        [TestCase(true, 15f)]
        [TestCase(false, 3f)]
        public void RadiusMatchesSenses(bool lightOn, float expected)
        {
            var ring = new WitchSightRing(new WitchSenseProfile(), lightOn);
            ring.Tick(1f, true, WitchForm.Grounded, lightOn, 30f);
            Assert.AreEqual(expected, ring.Radius);
        }

        [Test]
        public void FlyingAndInactiveFadeOut()
        {
            foreach (bool active in new[] { true, false })
            {
                var ring = new WitchSightRing(new WitchSenseProfile(), false);
                ring.Tick(0.25f, true, WitchForm.Grounded, false, 30f);
                ring.Tick(0.1f, active, active ? WitchForm.Flying : WitchForm.Grounded, false, 30f);
                Assert.IsFalse(ring.Visible);
                Assert.That(ring.Alpha, Is.InRange(0.01f, 0.17f));
                ring.Tick(0.25f, active, WitchForm.Flying, false, 30f);
                Assert.AreEqual(0f, ring.Alpha);
            }
        }

        [Test]
        public void InsideRadiusIntensifiesAndPulses()
        {
            var ring = new WitchSightRing(new WitchSenseProfile(), true);
            ring.Tick(1f, true, WitchForm.Grounded, true, 16f);
            Assert.AreEqual(0.18f, ring.Alpha);
            ring.Tick(0.1f, true, WitchForm.Grounded, true, 15f);
            float alpha = ring.Alpha;
            Assert.That(alpha, Is.InRange(0.425f, 0.475f));
            ring.Tick(0.1f, true, WitchForm.Grounded, true, 15f);
            Assert.AreNotEqual(alpha, ring.Alpha);
        }

        [TestCase(true, 3f, 15f)]
        [TestCase(false, 15f, 3f)]
        public void RadiusAndAppearanceTransitionSmoothly(bool lightOn, float from, float to)
        {
            var ring = new WitchSightRing(new WitchSenseProfile(), !lightOn);
            ring.Tick(0.1f, true, WitchForm.Grounded, lightOn, 30f);
            Assert.AreEqual(from + (to - from) * 0.15625f, ring.Radius, 0.001f);
            Assert.That(ring.Alpha, Is.InRange(0.01f, 0.17f));
            ring.Tick(0.3f, true, WitchForm.Grounded, lightOn, 30f);
            Assert.AreEqual(to, ring.Radius);
            Assert.AreEqual(0.18f, ring.Alpha);
        }

        [Test]
        public void ReversingTransitionKeepsCurrentRadius()
        {
            var ring = new WitchSightRing(new WitchSenseProfile(), false);
            ring.Tick(0.2f, true, WitchForm.Grounded, true, 30f);
            float radius = ring.Radius;
            ring.Tick(0f, true, WitchForm.Grounded, false, 30f);
            Assert.AreEqual(radius, ring.Radius);
            ring.Tick(0.4f, true, WitchForm.Grounded, false, 30f);
            Assert.AreEqual(3f, ring.Radius);
        }
    }
}
