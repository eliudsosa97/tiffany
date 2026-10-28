using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WitchMimicryTests
    {
        [Test]
        public void LureResetsDelayButKeepsCountAndShortensIntervals()
        {
            var mimicry = new WitchMimicry();
            Assert.IsFalse(mimicry.Tick(100f, false));
            Assert.AreEqual(0, mimicry.Count);
            Assert.AreEqual(WitchMimicry.MaxInterval, mimicry.Interval);
            Assert.IsFalse(mimicry.Tick(2.25f, true));
            Assert.IsTrue(mimicry.Tick(0.25f, true));
            Assert.AreEqual(1, mimicry.Count);
            float interval = mimicry.Interval;
            Assert.Less(interval, WitchMimicry.MaxInterval);
            Assert.IsFalse(mimicry.Tick(interval - 0.25f, true));
            Assert.IsTrue(mimicry.Tick(0.25f, true));
            Assert.Less(mimicry.Interval, interval);
            Assert.IsFalse(mimicry.Tick(1f, true));
            Assert.IsFalse(mimicry.Tick(100f, false));
            Assert.AreEqual(2, mimicry.Count);
            Assert.IsFalse(mimicry.Tick(2.25f, true));
            Assert.IsTrue(mimicry.Tick(0.25f, true));
            Assert.AreEqual(3, mimicry.Count);
        }

        [TestCase(0, 0f, 0.92f, 0.18f, 0.25f, 9f)]
        [TestCase(3, 0.5f, 0.77f, 0.315f, 0.425f, 7f)]
        [TestCase(6, 1f, 0.62f, 0.45f, 0.6f, 5f)]
        [TestCase(12, 1f, 0.62f, 0.45f, 0.6f, 5f)]
        public void DeformationSaturates(int count, float distortion, float pitch, float delay, float volume, float interval)
        {
            var mimicry = new WitchMimicry();
            for (int i = 0; i < count; i++) Assert.IsTrue(mimicry.Tick(WitchMimicry.MaxInterval, true));
            Assert.AreEqual(count, mimicry.Count);
            Assert.AreEqual(distortion, mimicry.Distortion, 1e-5f);
            Assert.AreEqual(pitch, mimicry.Pitch, 1e-5f);
            Assert.AreEqual(delay, mimicry.EchoDelay, 1e-5f);
            Assert.AreEqual(volume, mimicry.EchoVolume, 1e-5f);
            Assert.AreEqual(interval, mimicry.Interval, 1e-5f);
        }

        [TestCase(1)]
        [TestCase(4)]
        public void ClipsCycleDeterministically(int clipCount)
        {
            var mimicry = new WitchMimicry();
            for (int i = 0; i < 20; i++) Assert.AreEqual(i % clipCount, mimicry.ClipIndex(clipCount));
            Assert.AreEqual(-1, mimicry.ClipIndex(0));
        }
    }
}
