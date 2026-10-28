using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class StepCadenceTests
    {
        [Test]
        public void FirstStepFallsAtHalfStride()
        {
            var cadence = new StepCadence();

            Assert.IsFalse(cadence.Advance(0.34f, Gait.Walk));
            Assert.IsTrue(cadence.Advance(0.02f, Gait.Walk));
        }

        [TestCase(Gait.Slow, 1.1f, 0.5f)]
        [TestCase(Gait.Walk, 2.2f, 0.7f)]
        [TestCase(Gait.Run, 4f, 1.1f)]
        public void TenSecondsMatchesDistanceAndStride(Gait gait, float speed, float stride)
        {
            Assert.AreEqual(stride, StepCadence.Stride(gait), 1e-4f);
            Assert.AreEqual(Mathf.FloorToInt(speed * 10f / stride + 0.5f), CountSteps(gait, speed), 1);
        }

        [Test]
        public void FasterGaitsProduceMoreSteps()
        {
            int slow = CountSteps(Gait.Slow, 1.1f);
            int walk = CountSteps(Gait.Walk, 2.2f);
            int run = CountSteps(Gait.Run, 4f);

            Assert.Greater(run, walk);
            Assert.Greater(walk, slow);
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        public void StoppingNeverDelaysNextStep(float distance)
        {
            var cadence = new StepCadence();

            Assert.IsFalse(cadence.Advance(0.3f, Gait.Walk));
            Assert.IsFalse(cadence.Advance(distance, Gait.Walk));
            Assert.IsTrue(cadence.Advance(0.3f, Gait.Walk));
        }

        [Test]
        public void StoppingRightAfterStepRestartsAtHalfStride()
        {
            var cadence = new StepCadence();

            Assert.IsTrue(cadence.Advance(0.36f, Gait.Walk));
            Assert.IsFalse(cadence.Advance(0f, Gait.Walk));
            Assert.IsFalse(cadence.Advance(0.34f, Gait.Walk));
            Assert.IsTrue(cadence.Advance(0.02f, Gait.Walk));
        }

        [Test]
        public void TappingForwardStillProducesSteps()
        {
            var cadence = new StepCadence();
            int steps = 0;
            for (int tap = 0; tap < 10; tap++)
            {
                if (cadence.Advance(0.22f, Gait.Walk)) steps++;
                Assert.IsFalse(cadence.Advance(0f, Gait.Walk));
            }

            Assert.GreaterOrEqual(steps, 2);
        }

        [Test]
        public void LargeDistanceProducesOneStepWithoutBacklog()
        {
            var cadence = new StepCadence();

            Assert.IsTrue(cadence.Advance(5f, Gait.Walk));
            Assert.IsFalse(cadence.Advance(0.01f, Gait.Walk));
        }

        [Test]
        public void GaitChangeUsesNewStrideForRemainingPhase()
        {
            var running = new StepCadence();
            var walking = new StepCadence();

            Assert.IsFalse(running.Advance(0.2f, Gait.Walk));
            Assert.IsFalse(walking.Advance(0.2f, Gait.Walk));
            Assert.IsFalse(running.Advance(0.2f, Gait.Run));
            Assert.IsTrue(walking.Advance(0.2f, Gait.Walk));
        }

        static int CountSteps(Gait gait, float speed)
        {
            var cadence = new StepCadence();
            int steps = 0;
            for (int frame = 0; frame < 600; frame++)
            {
                if (cadence.Advance(speed / 60f, gait)) steps++;
            }

            return steps;
        }
    }
}
