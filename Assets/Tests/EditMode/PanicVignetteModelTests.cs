using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class PanicVignetteModelTests
    {
        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.15625f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.75f, 0.84375f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void CurveClampsAndClosesProgressively(float panic, float expected)
        {
            Assert.AreEqual(expected, PanicVignetteModel.Intensity(panic), 1e-6f);
        }

        [Test]
        public void PanicClosesEdgesSmoothlyAndCalmingRestoresVisibility()
        {
            var model = new PanicVignetteModel();
            Assert.AreEqual(0f, model.Alpha);
            Assert.AreEqual(1.6f, model.Scale);
            model.Advance(1f, 0.05f);
            Assert.That(model.Alpha, Is.InRange(0.01f, 0.3f));
            Assert.That(model.Scale, Is.InRange(1.3f, 1.59f));
            model.Advance(1f, 5f);
            Assert.AreEqual(0.85f, model.Alpha, 1e-5f);
            Assert.AreEqual(1f, model.Scale, 1e-5f);
            model.Advance(0f, 0.05f);
            Assert.That(model.Alpha, Is.InRange(0.5f, 0.84f));
            model.Advance(0f, 5f);
            Assert.AreEqual(0f, model.Alpha, 1e-5f);
            Assert.AreEqual(1.6f, model.Scale, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void PausedOrNegativeTimePreservesTheOverlay(float dt)
        {
            var model = new PanicVignetteModel();
            model.Advance(0.8f, 0.3f);
            float alpha = model.Alpha;
            float scale = model.Scale;
            model.Advance(0f, dt);
            Assert.AreEqual(alpha, model.Alpha);
            Assert.AreEqual(scale, model.Scale);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void SmoothingDoesNotDependOnFrameRate(int fps)
        {
            var frames = new PanicVignetteModel();
            var oneStep = new PanicVignetteModel();
            for (int i = 0; i < fps; i++) frames.Advance(0.9f, 1f / fps);
            oneStep.Advance(0.9f, 1f);
            Assert.AreEqual(oneStep.Alpha, frames.Alpha, 1e-5f);
            Assert.AreEqual(oneStep.Scale, frames.Scale, 1e-5f);
        }
    }
}
