using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class LampFlickerTests
    {
        [TestCase(0)]
        [TestCase(127)]
        [TestCase(-127)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void IntensityStaysInRange(int seed)
        {
            var flicker = new LampFlicker(seed);
            for (int i = 0; i < 12000; i++)
                Assert.That(flicker.Tick(0.01f), Is.InRange(LampFlicker.MinIntensity, 1f));
        }

        [Test]
        public void SameSeedAndFrameTimesProduceTheSameValues()
        {
            var first = new LampFlicker(127);
            var second = new LampFlicker(127);
            for (int i = 0; i < 10000; i++)
            {
                float dt = i % 3 == 0 ? 0.02f : 0.007f;
                Assert.AreEqual(first.Tick(dt), second.Tick(dt));
            }
        }

        [Test]
        public void HumVariesBeforeTheFirstDropAndDependsOnTheSeed()
        {
            var first = new LampFlicker(1);
            var second = new LampFlicker(2);
            float min = 1f, max = 0f;
            bool different = false;
            for (int i = 0; i < 300; i++)
            {
                float value = first.Tick(0.01f);
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                different |= value != second.Tick(0.01f);
            }
            Assert.Greater(max - min, 0.02f);
            Assert.GreaterOrEqual(min, 1f - LampFlicker.HumAmplitude);
            Assert.IsTrue(different);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(127)]
        [TestCase(-127)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void DropsAreBriefAndRareOverTwoMinutes(int seed)
        {
            var flicker = new LampFlicker(seed);
            int drops = 0, darkFrames = 0, currentFrames = 0;
            float previousStart = -100f;
            for (int i = 0; i < 12000; i++)
            {
                bool dark = flicker.Tick(0.01f) < 0.85f;
                if (dark)
                {
                    if (currentFrames == 0)
                    {
                        float start = (i + 1) * 0.01f;
                        if (drops > 0)
                            Assert.That(start - previousStart, Is.InRange(3.9f, 12.1f));
                        previousStart = start;
                        drops++;
                    }
                    currentFrames++;
                    darkFrames++;
                }
                else if (currentFrames > 0)
                {
                    Assert.That(currentFrames * 0.01f, Is.InRange(0.03f, LampFlicker.MaxDropDuration));
                    currentFrames = 0;
                }
            }
            Assert.That(drops, Is.InRange(8, 30));
            Assert.That(darkFrames, Is.InRange(1, 1200));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTimePreservesTheValueAndFutureSequence(float dt)
        {
            var flicker = new LampFlicker(127);
            var control = new LampFlicker(127);
            Assert.AreEqual(control.Tick(0f), flicker.Tick(dt));
            float current = 1f;
            for (int i = 0; i < 1300 && current >= 0.5f; i++)
            {
                current = control.Tick(0.01f);
                Assert.AreEqual(current, flicker.Tick(0.01f));
            }
            Assert.Less(current, 0.5f);
            for (int i = 0; i < 10; i++) Assert.AreEqual(current, flicker.Tick(dt));
            for (int i = 0; i < 3000; i++) Assert.AreEqual(control.Tick(0.01f), flicker.Tick(0.01f));
        }

        [Test]
        public void LargeTicksPreserveTheDropSchedule()
        {
            var stepped = new LampFlicker(127);
            var jumped = new LampFlicker(127);
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 80; j++) stepped.Tick(0.25f);
                Assert.AreEqual(stepped.Tick(0f), jumped.Tick(20f));
            }
        }

        [Test]
        public void TenThousandTicksAllocateNoMemory()
        {
            var flicker = new LampFlicker(127);
            for (int i = 0; i < 1000; i++) flicker.Tick(0.01f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) flicker.Tick(0.01f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }
    }
}
