using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class StormCycleTests
    {
        StormCycle cycle;

        [SetUp] public void SetUp() => cycle = new StormCycle(() => 0f);

        [Test] public void DryWeatherHasNoRandomStrikes() => Check(100f);

        [Test]
        public void RainStrikesAtTwelveAndTwentyFourSeconds()
        {
            cycle.Active = true;
            Check(11f);
            Check(1f, 1f);
            Check(11f, started: true);
            Check(1f, 1f);
            Check(0f, 1f);
        }

        [TestCase(1.199f, false)]
        [TestCase(1.2f, true)]
        public void MidpointRandomStrikesAtTwentyOneSecondsAndThundersAfterOnePointTwoSeconds(float elapsed, bool thunder)
        {
            cycle = new StormCycle(() => 0.5f);
            cycle.Active = true;
            Check(20f);
            Check(1f, 1f);
            Check(elapsed, thunder: thunder, started: thunder);
            Check(0f, thunder: thunder);
        }

        [TestCase(0f, 1f, false)]
        [TestCase(0.079f, 1f, false), TestCase(0.08f, 0.35f, false)]
        [TestCase(0.159f, 0.35f, false), TestCase(0.16f, 1f, false)]
        [TestCase(0.399f, 1f, false), TestCase(0.4f, 1f, true)]
        [TestCase(0.999f, 1f, true), TestCase(1f, 0f, true)]
        [TestCase(3.399f, 0f, true), TestCase(3.4f, 0f, false)]
        public void DryStrikeFlickersThenThundersWithStartFlagForOneTick(float elapsed, float level, bool thunder)
        {
            cycle.Strike();
            Check(elapsed, level, thunder, started: elapsed >= 0.4f);
            Check(0f, level, thunder);
        }

        [TestCase(0.2f)]
        [TestCase(0.5f)]
        public void StoppingRainCancelsFlashPendingThunderAndSound(float elapsed)
        {
            cycle.Active = true;
            cycle.Strike();
            cycle.Tick(elapsed);
            cycle.Active = false;
            Check(0f);
            Check(100f);
            cycle.Active = true;
            Check(11f);
            Check(1f, 1f);
        }

        [Test]
        public void NewThunderRestartsItsDuration()
        {
            cycle.Strike();
            cycle.Tick(2f);
            cycle.Strike();
            Check(0.4f, 1f, thunder: true, started: true);
            Check(2f, thunder: true);
            Check(1f);
        }

        void Check(float dt, float level = 0f, bool thunder = false, bool started = false)
        {
            cycle.Tick(dt);
            Assert.AreEqual((level, level > 0f, thunder, started),
                (cycle.FlashLevel, cycle.Flashing, cycle.ThunderSounding, cycle.ThunderStartedThisTick));
        }
    }
}
