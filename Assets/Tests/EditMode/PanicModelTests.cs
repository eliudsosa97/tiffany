using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class PanicModelTests
    {
        [Test]
        public void DarknessAloneRaisesPanic()
        {
            var panic = new PanicModel();
            var inputs = new PanicInputs
            {
                InDarkness = true,
                WitchDistance = float.PositiveInfinity
            };

            panic.Tick(5f, in inputs);

            Assert.AreEqual(0.06f, panic.Level, 1e-4f);
        }

        [TestCase(true, false, false, 0.4f)]
        [TestCase(false, true, false, 0.52f)]
        [TestCase(false, false, true, 0.5f)]
        [TestCase(true, true, true, 0.22f)]
        public void CalmingInputsReducePanic(bool safeLight, bool hiddenAndStill, bool dogNear, float expected)
        {
            var panic = new PanicModel();
            panic.ResetTo(0.6f);
            var inputs = new PanicInputs
            {
                WitchDistance = float.PositiveInfinity,
                InSafeLight = safeLight,
                HiddenAndStill = hiddenAndStill,
                DogNear = dogNear
            };

            panic.Tick(2f, in inputs);

            Assert.AreEqual(expected, panic.Level, 1e-4f);
        }

        [Test]
        public void NoneInputsPreservePanic()
        {
            var panic = new PanicModel();
            panic.ResetTo(0.6f);
            var inputs = PanicInputs.None;

            panic.Tick(10f, in inputs);

            Assert.AreEqual(0.6f, panic.Level, 1e-4f);
        }

        [TestCase(0f, 0.3f)]
        [TestCase(10f, 0.15f)]
        [TestCase(20f, 0f)]
        [TestCase(25f, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.NaN, 0f)]
        public void WitchRaisesPanicWithinRadius(float distance, float expected)
        {
            var panic = new PanicModel();
            var inputs = new PanicInputs { WitchDistance = distance };

            panic.Tick(5f, in inputs);

            Assert.AreEqual(expected, panic.Level, 1e-4f);
        }

        [TestCase(false, 0f, false, 0.3f)]
        [TestCase(true, 0f, false, 0.51f)]
        [TestCase(false, float.PositiveInfinity, true, 0.15f)]
        [TestCase(true, float.PositiveInfinity, true, 0.285f)]
        public void DarknessAmplifiesWitchAndWhispers(bool darkness, float distance, bool whispers, float expected)
        {
            var panic = new PanicModel();
            var inputs = new PanicInputs
            {
                InDarkness = darkness,
                WitchDistance = distance,
                HearingWhispers = whispers
            };

            panic.Tick(5f, in inputs);

            Assert.AreEqual(expected, panic.Level, 1e-4f);
        }

        [Test]
        public void SimultaneousRisesAndCalmingAreAddedTogether()
        {
            var panic = new PanicModel();
            panic.ResetTo(0.5f);
            var inputs = new PanicInputs
            {
                InDarkness = true,
                WitchDistance = 10f,
                HearingWhispers = true,
                HiddenAndStill = true,
                DogNear = true
            };

            panic.Tick(2f, in inputs);

            Assert.AreEqual(0.524f, panic.Level, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimeDoesNotChangePanic(float dt)
        {
            var panic = new PanicModel();
            panic.ResetTo(0.5f);
            var inputs = new PanicInputs { InDarkness = true, WitchDistance = 0f };

            panic.Tick(dt, in inputs);

            Assert.AreEqual(0.5f, panic.Level, 1e-4f);
        }

        [Test]
        public void LargeTimeStepsClampPanicInBothDirections()
        {
            var panic = new PanicModel();
            var inputs = new PanicInputs
            {
                InDarkness = true,
                WitchDistance = float.PositiveInfinity
            };

            panic.Tick(1000f, in inputs);
            Assert.AreEqual(1f, panic.Level, 1e-4f);

            inputs.InDarkness = false;
            inputs.InSafeLight = true;
            panic.Tick(1000f, in inputs);
            Assert.AreEqual(0f, panic.Level, 1e-4f);
        }

        [TestCase(0f, PanicStage.Calm)]
        [TestCase(0.49f, PanicStage.Calm)]
        [TestCase(0.5f, PanicStage.Steps)]
        [TestCase(0.74f, PanicStage.Steps)]
        [TestCase(0.75f, PanicStage.FalseFires)]
        [TestCase(0.89f, PanicStage.FalseFires)]
        [TestCase(0.9f, PanicStage.Children)]
        [TestCase(1f, PanicStage.Children)]
        public void StageMatchesThreshold(float level, PanicStage expected)
        {
            var panic = new PanicModel();

            panic.ResetTo(level);

            Assert.AreEqual(expected, panic.Stage);
        }

        [TestCase(0.3f, 0.8f, PanicStage.FalseFires)]
        [TestCase(100f, 1f, PanicStage.Children)]
        public void SpikeRaisesPanicAndChangesStage(float amount, float expected, PanicStage stage)
        {
            var panic = new PanicModel();
            panic.ResetTo(0.5f);

            panic.Spike(amount);

            Assert.AreEqual(expected, panic.Level, 1e-4f);
            Assert.AreEqual(stage, panic.Stage);
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(2f, 1f)]
        public void ResetToClampsPanic(float level, float expected)
        {
            var panic = new PanicModel();

            panic.ResetTo(level);

            Assert.AreEqual(expected, panic.Level, 1e-4f);
        }
    }
}
