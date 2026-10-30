using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class HeartbeatRateTests
    {
        [TestCase(true, WitchForm.Grounded, 4f)]
        [TestCase(true, WitchForm.Flying, float.PositiveInfinity)]
        [TestCase(false, WitchForm.Grounded, float.PositiveInfinity)]
        [TestCase(false, WitchForm.Flying, float.PositiveInfinity)]
        public void OnlyActiveGroundedWitchContributesDistance(bool active, WitchForm form, float expected) =>
            Assert.AreEqual(expected, HeartbeatRate.WitchDistance(active, form, 4f));

        [TestCase(0f, 40f, 0f, 0f, 0f)]
        [TestCase(0f, PanicModel.WitchRadius, 0f, 0f, 0f)]
        [TestCase(0f, float.PositiveInfinity, 0f, 0f, 0f)]
        [TestCase(-1f, 40f, 0f, 0f, 0f)]
        [TestCase(0f, 3f, 1f, 140f, 1f)]
        [TestCase(0f, 0f, 1f, 140f, 1f)]
        [TestCase(0.5f, 40f, 0.5f, 90f, 0.4642857f)]
        [TestCase(0.3f, 11.5f, 0.5f, 90f, 0.4642857f)]
        [TestCase(0.5f, 15f, 0.5f, 90f, 0.4642857f)]
        [TestCase(0.2999f, 40f, 0.2999f, 0f, 0f)]
        [TestCase(HeartbeatRate.Audible, 40f, 0.3f, 70f, 0.25f)]
        [TestCase(0.3001f, 40f, 0.3001f, 70.01f, 0.2501071f)]
        [TestCase(2f, 40f, 1f, 140f, 1f)]
        public void PanicAndProximityUseTheGreaterIntensity(float panic, float distance, float intensity, float bpm, float volume)
        {
            float actual = HeartbeatRate.Intensity(panic, distance);
            Assert.AreEqual(intensity, actual, 1e-4f);
            Assert.AreEqual(bpm, HeartbeatRate.BeatsPerMinute(actual), 1e-4f);
            Assert.AreEqual(volume, HeartbeatRate.Volume(actual), 1e-4f);
        }

        [TestCase(65.4f, 55, false)]
        [TestCase(65.6f, 55, true)]
        [TestCase(44f, 55, true)]
        [TestCase(0f, 55, true)]
        public void LogsOnlyIntegerChangesGreaterThanTen(float bpm, int previous, bool expected) =>
            Assert.AreEqual(expected, HeartbeatRate.ShouldLog(bpm, previous));

        [Test]
        public void CadencePausesAndSilenceRestartsWithAnImmediateBeat()
        {
            var rate = new HeartbeatRate();
            Assert.IsFalse(rate.Tick(0f, 60f));
            Assert.IsTrue(rate.Tick(0.01f, 60f));
            Assert.IsFalse(rate.Tick(0.5f, 60f));
            Assert.IsFalse(rate.Tick(0f, 60f));
            Assert.IsTrue(rate.Tick(0.5f, 60f));
            Assert.IsFalse(rate.Tick(0.01f, 0f));
            Assert.IsTrue(rate.Tick(0.01f, 60f));
        }
    }
}
