using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class CaughtSequenceTests
    {
        [Test]
        public void BeginRunsFireFadeOutFadeInAndIdleAtTheirDurations()
        {
            var sequence = new CaughtSequence();
            Assert.AreEqual(CaughtPhase.Idle, sequence.Phase);
            Assert.AreEqual(0f, sequence.Overlay);
            sequence.Begin();
            Assert.AreEqual(CaughtPhase.Fire, sequence.Phase);
            Assert.AreEqual(1f, sequence.FireAmount);
            sequence.Tick(CaughtSequence.FireDuration);
            Assert.AreEqual(CaughtPhase.FadeOut, sequence.Phase);
            Assert.AreEqual(1f, sequence.Overlay);
            sequence.Tick(CaughtSequence.FadeOutDuration * 0.5f);
            Assert.AreEqual(CaughtPhase.FadeOut, sequence.Phase);
            Assert.AreEqual(1f, sequence.Overlay);
            sequence.Tick(CaughtSequence.FadeOutDuration * 0.5f);
            Assert.AreEqual(CaughtPhase.FadeIn, sequence.Phase);
            Assert.IsTrue(sequence.RespawnDue);
            Assert.AreEqual(0f, sequence.FireAmount);
            sequence.Tick(CaughtSequence.FadeInDuration * 0.5f);
            Assert.AreEqual(0.5f, sequence.Overlay, 1e-5f);
            Assert.IsFalse(sequence.RespawnDue);
            sequence.Tick(CaughtSequence.FadeInDuration * 0.5f);
            Assert.AreEqual(CaughtPhase.Idle, sequence.Phase);
            Assert.IsFalse(sequence.Active);
            Assert.AreEqual(0f, sequence.Overlay);
        }

        [TestCase(0.01f)]
        [TestCase(CaughtSequence.FireDuration + CaughtSequence.FadeOutDuration)]
        [TestCase(10f)]
        public void RespawnIsDueOncePerSequenceEvenAcrossMultiplePhases(float dt)
        {
            var sequence = new CaughtSequence();
            for (int run = 0; run < 2; run++)
            {
                sequence.Begin();
                int count = 0;
                while (sequence.Active)
                {
                    sequence.Tick(dt);
                    if (sequence.RespawnDue) count++;
                }
                Assert.AreEqual(1, count);
                sequence.Tick(0f);
                Assert.IsFalse(sequence.RespawnDue);
            }
        }

        [Test]
        public void BeginDuringSequenceDoesNotRestartIt()
        {
            var sequence = new CaughtSequence();
            sequence.Begin();
            sequence.Tick(CaughtSequence.FireDuration + CaughtSequence.FadeOutDuration * 0.5f);
            float fire = sequence.FireAmount;
            sequence.Begin();
            Assert.AreEqual(CaughtPhase.FadeOut, sequence.Phase);
            Assert.AreEqual(fire, sequence.FireAmount);
            sequence.Tick(CaughtSequence.FadeOutDuration * 0.5f);
            Assert.IsTrue(sequence.RespawnDue);
        }
    }
}
