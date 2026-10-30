using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class FootstepSoundsTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void InterleavedGroundsNeverRepeatAndUseEveryClip(int count)
        {
            var sounds = new FootstepSounds(new[] { count, count, count, count, count, count });
            var random = new System.Random(15);
            var previous = new int[6];
            var used = new bool[6, count];
            for (int i = 0; i < 6000; i++)
            {
                int ground = i % 6;
                var sound = sounds.Choose((GroundType)ground, Gait.Walk, (float)random.NextDouble());
                Assert.AreEqual((GroundType)ground, sound.Ground);
                Assert.AreNotEqual(previous[ground] - 1, sound.Index);
                previous[ground] = sound.Index + 1;
                used[ground, sound.Index] = true;
            }
            foreach (bool seen in used) Assert.IsTrue(seen);
        }

        [TestCase(Gait.Slow, 0.175f)]
        [TestCase(Gait.Walk, 0.3f)]
        [TestCase(Gait.Run, 0.5f)]
        public void GaitControlsVolumeAndPitchStaysWithinFivePercent(Gait gait, float volume)
        {
            var sounds = new FootstepSounds(new[] { 4, 4, 4, 4, 4, 4 });
            for (int i = 0; i <= 100; i++)
            {
                var sound = sounds.Choose(GroundType.Dirt, gait, i / 100f);
                Assert.AreEqual(volume, sound.Volume);
                Assert.That(sound.Pitch, Is.InRange(0.95f, 1.05f));
            }
        }

        [Test]
        public void VolumesCanBeAdjusted()
        {
            var sounds = new FootstepSounds(new[] { 1 }, 0.2f, 0.4f, 0.8f);
            foreach (Gait gait in System.Enum.GetValues(typeof(Gait)))
                Assert.AreEqual(0.1f * (1 << (int)gait), sounds.Choose(GroundType.Dirt, gait, 0f).Volume);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        public void MissingGroundUsesDirtAndSharesItsHistory(int count)
        {
            var sounds = new FootstepSounds(new[] { count, 0, 0, 0, 0, 0 });
            var dirt = sounds.Choose(GroundType.Dirt, Gait.Walk, 0f);
            var fallback = sounds.Choose(GroundType.Gravel, Gait.Walk, 0f);
            Assert.AreEqual(GroundType.Dirt, fallback.Ground);
            Assert.AreEqual(count == 0 ? -1 : count == 1 ? 0 : 1, fallback.Index);
            if (count > 1) Assert.AreNotEqual(dirt.Index, fallback.Index);
        }
    }
}
