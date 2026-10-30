using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class PanicEffectsTests
    {
        private PanicEffects effects;

        [SetUp]
        public void SetUp() => effects = new PanicEffects(new System.Random(17));

        [TestCase(PanicStage.Steps, 4f, 8f)]
        [TestCase(PanicStage.FalseFires, 6f, 10f)]
        [TestCase(PanicStage.Children, 4f, 4f)]
        public void CuesRepeatWithinTheirIntervalsAndSpacing(PanicStage stage, float min, float max)
        {
            if (stage > PanicStage.Steps) max += PanicEffects.CueGap;
            Assert.IsFalse(Cue(effects.Tick(0f, stage, 0f, false), stage));
            float time = 0f;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                float elapsed = 0f;
                bool fired = false;
                while (elapsed < max && !fired)
                {
                    elapsed += 0.25f;
                    time += 0.25f;
                    fired = Cue(effects.Tick(0.25f, stage, time, false), stage);
                    if (elapsed < min) Assert.IsFalse(fired);
                }
                Assert.IsTrue(fired);
                Assert.That(elapsed, Is.InRange(min, max));
                Assert.IsFalse(Cue(effects.Tick(0f, stage, time, false), stage));
            }
        }

        [Test]
        public void ChildrenCuesStaySpacedAndIncludeAllTypes()
        {
            const float dt = 0.02f;
            int previousCueFrame = -1;
            bool step = false, whisper = false, sob = false;
            for (int frame = 1; frame <= 15000; frame++)
            {
                PanicCues cues = effects.Tick(dt, PanicStage.Children, frame * dt, false);
                int count = CueCount(cues);
                if (count == 0) continue;
                Assert.LessOrEqual(count, 1);
                if (previousCueFrame >= 0)
                    Assert.GreaterOrEqual((frame - previousCueFrame) * dt, 2.5f);
                previousCueFrame = frame;
                step |= cues.FakeStep;
                whisper |= cues.Whisper;
                sob |= cues.Sob;
            }
            Assert.IsTrue(step);
            Assert.IsTrue(whisper);
            Assert.IsTrue(sob);
        }

        [TestCase(0.02f)]
        [TestCase(10f)]
        public void AtMostOneCueFiresPerTick(float dt)
        {
            for (int frame = 1; frame <= 1000; frame++)
                Assert.LessOrEqual(CueCount(effects.Tick(dt, PanicStage.Children, frame * dt, false)), 1);
        }

        [Test]
        public void DeferredCuesKeepPriorityAndFireWhenGapEnds()
        {
            float time = PanicEffects.WhisperMaxInterval;
            Assert.IsTrue(effects.Tick(time, PanicStage.Children, time, false).Sob);
            time += PanicEffects.CueGap - 0.25f;
            Assert.AreEqual(0, CueCount(effects.Tick(PanicEffects.CueGap - 0.25f,
                PanicStage.Children, time, false)));
            time += 0.25f;
            Assert.IsTrue(effects.Tick(0.25f, PanicStage.Children, time, false).Whisper);
            time += 2f;
            Assert.AreEqual(0, CueCount(effects.Tick(2f, PanicStage.Children, time, false)));
            time += 0.25f;
            Assert.AreEqual(0, CueCount(effects.Tick(0.25f, PanicStage.Children, time, false)));
            time += 0.25f;
            Assert.IsTrue(effects.Tick(0.25f, PanicStage.Children, time, false).Sob);
            time += PanicEffects.CueGap;
            Assert.IsTrue(effects.Tick(PanicEffects.CueGap, PanicStage.Children, time, false).FakeStep);
        }

        [TestCase(PanicStage.Steps, PanicStage.Calm)]
        [TestCase(PanicStage.FalseFires, PanicStage.Steps)]
        public void FallingBelowThresholdDiscardsPendingCue(PanicStage stage, PanicStage lower)
        {
            float time = PanicEffects.WhisperMaxInterval;
            effects.Tick(time, PanicStage.Children, time, false);
            time += PanicEffects.CueGap;
            Assert.IsFalse(Cue(effects.Tick(PanicEffects.CueGap, lower, time, false), stage));
            time += PanicEffects.CueGap;
            Assert.IsFalse(Cue(effects.Tick(PanicEffects.CueGap, stage, time, false), stage));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InactiveSobDiscardsPendingCue(bool hidden)
        {
            float time = PanicEffects.WhisperMaxInterval;
            effects.Tick(time, PanicStage.Children, time, false);
            time += PanicEffects.CueGap;
            effects.Tick(PanicEffects.CueGap, PanicStage.Children, time, false);
            time += 2f;
            Assert.IsFalse(effects.Tick(2f, PanicStage.Children, time, false).Sob);
            var stage = hidden ? PanicStage.Children : PanicStage.FalseFires;
            time += 0.5f;
            Assert.IsFalse(effects.Tick(0.5f, stage, time, hidden).Sob);
            time += PanicEffects.CueGap;
            Assert.IsFalse(effects.Tick(PanicEffects.CueGap, PanicStage.Children, time, false).Sob);
        }

        [TestCase(PanicStage.Calm)]
        [TestCase(PanicStage.Steps)]
        [TestCase(PanicStage.FalseFires)]
        [TestCase(PanicStage.Children)]
        public void StagesGateCuesAndLight(PanicStage stage)
        {
            bool step = false, whisper = false, sob = false;
            float minScale = 1f, maxScale = 0f;
            for (int frame = 0; frame < 320; frame++)
            {
                PanicCues cues = effects.Tick(0.125f, stage, frame * 0.125f, false);
                step |= cues.FakeStep;
                whisper |= cues.Whisper;
                sob |= cues.Sob;
                if (stage < PanicStage.Children) Assert.AreEqual(1f, cues.LightScale);
                else Assert.That(cues.LightScale, Is.InRange(0.55f, 1f));
                minScale = System.Math.Min(minScale, cues.LightScale);
                maxScale = System.Math.Max(maxScale, cues.LightScale);
            }
            Assert.AreEqual(stage >= PanicStage.Steps, step);
            Assert.AreEqual(stage >= PanicStage.FalseFires, whisper);
            Assert.AreEqual(stage >= PanicStage.Children, sob);
            if (stage == PanicStage.Children) Assert.Less(minScale, maxScale);
        }

        [TestCase(PanicStage.Steps, 4f, 8f)]
        [TestCase(PanicStage.FalseFires, 6f, 10f)]
        [TestCase(PanicStage.Children, 4f, 4f)]
        public void FallingBelowThresholdStopsAndResetsCue(PanicStage stage, float min, float max)
        {
            effects.Tick(3f, stage, 3f, false);
            var lower = (PanicStage)((int)stage - 1);
            Assert.IsFalse(Cue(effects.Tick(20f, lower, 23f, false), stage));
            Assert.IsFalse(Cue(effects.Tick(min - 0.25f, stage, 24f, false), stage));
            PanicCues cues = effects.Tick(max - min + 0.25f, stage, 32f, false);
            if (!Cue(cues, stage)) cues = effects.Tick(PanicEffects.CueGap, stage, 32f + PanicEffects.CueGap, false);
            Assert.IsTrue(Cue(cues, stage));
        }

        [Test]
        public void HidingStopsAndResetsSob()
        {
            Assert.IsFalse(effects.Tick(3f, PanicStage.Children, 3f, false).Sob);
            for (int frame = 1; frame <= 40; frame++)
                Assert.IsFalse(effects.Tick(0.25f, PanicStage.Children, 3f + frame * 0.25f, true).Sob);
            float time = 13f + PanicEffects.SobInterval;
            Assert.IsFalse(effects.Tick(PanicEffects.SobInterval - 0.25f, PanicStage.Children, time - 0.25f, false).Sob);
            PanicCues cues = effects.Tick(0.25f, PanicStage.Children, time, false);
            if (!cues.Sob) cues = effects.Tick(PanicEffects.CueGap, PanicStage.Children, time + PanicEffects.CueGap, false);
            Assert.IsTrue(cues.Sob);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimeDoesNotAdvanceCues(float dt)
        {
            for (int i = 0; i < 20; i++)
            {
                PanicCues cues = effects.Tick(dt, PanicStage.Children, i, false);
                Assert.IsFalse(cues.FakeStep || cues.Whisper || cues.Sob);
            }
        }

        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        public void DarknessNeedsBothLightAndSafeZoneAbsent(bool light, bool safe, bool dark)
        {
            PanicInputs inputs = PanicSenses.Build(light, safe, 7.5f, false, false, false, false);
            Assert.AreEqual(dark, inputs.InDarkness);
            Assert.AreEqual(safe, inputs.InSafeLight);
        }

        [Test]
        public void SensesMapVoiceDistanceDogAndHiddenStill([Values] bool hidden, [Values] bool still,
            [Values] bool voice, [Values] bool dog)
        {
            PanicInputs inputs = PanicSenses.Build(true, false, 7.5f, voice, hidden, still, dog);
            Assert.AreEqual(7.5f, inputs.WitchDistance);
            Assert.AreEqual(voice, inputs.HearingWhispers);
            Assert.AreEqual(hidden && still, inputs.HiddenAndStill);
            Assert.AreEqual(dog, inputs.DogNear);
        }

        private static bool Cue(PanicCues cues, PanicStage stage) => stage == PanicStage.Steps
            ? cues.FakeStep : stage == PanicStage.FalseFires ? cues.Whisper : cues.Sob;

        private static int CueCount(PanicCues cues) =>
            (cues.FakeStep ? 1 : 0) + (cues.Whisper ? 1 : 0) + (cues.Sob ? 1 : 0);
    }
}
