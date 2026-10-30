using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class WitchSoundRulesTests
    {
        private WitchSoundRules rules;
        [SetUp] public void SetUp() => rules = new WitchSoundRules();
        private bool HasEvent => rules.Landed || rules.Crack || rules.Whoosh || rules.Cackle;
        private void Tick(WitchState state, float dt = 1f) => rules.Tick(dt,
            new WitchDecision { State = state, Form = WitchForm.Grounded }, 0f);
        [TestCase(WitchForm.Flying, WitchState.Flying, true, 6f, 1f, 0f)] [TestCase(WitchForm.Flying, WitchState.Flying, true, 23f, 0.5f, 0f)]
        [TestCase(WitchForm.Flying, WitchState.Flying, true, 40f, 0f, 0f)] [TestCase(WitchForm.Flying, WitchState.Flying, false, 0f, 0f, 0f)]
        [TestCase(WitchForm.Flying, WitchState.Stalking, false, 0f, 0f, 0f)]
        [TestCase(WitchForm.Grounded, WitchState.Stalking, true, 3f, 0f, 1f)]
        [TestCase(WitchForm.Grounded, WitchState.WindUp, false, 9f, 0f, 0.5f)]
        [TestCase(WitchForm.Grounded, WitchState.Recovering, false, 15f, 0f, 0f)] [TestCase(WitchForm.Grounded, WitchState.Recovering, false, 0f, 0f, 1f)]
        [TestCase(WitchForm.Grounded, WitchState.Sprinting, false, 0f, 0f, 0f)]
        [TestCase(WitchForm.Grounded, WitchState.TakingOff, true, 0f, 0f, 0f)]
        public void LoopsFollowFormStateFireAndDistance(WitchForm form, WitchState state, bool lit, float distance, float fire, float breath)
        {
            rules.Tick(1f, new WitchDecision { Form = form, State = state, FireLit = lit }, distance);
            Assert.AreEqual((fire, breath), (rules.FireVolume, rules.BreathVolume));
            Assert.AreEqual(WitchSoundRules.Volume(distance, 5f, 30f), rules.OneShotVolume);
        }
        [TestCase(-1f, 1f)] [TestCase(5f, 1f)] [TestCase(17.5f, 0.5f)]
        [TestCase(30f, 0f)] [TestCase(float.PositiveInfinity, 0f)]
        public void VolumeClampsAtEdges(float distance, float expected) => Assert.AreEqual(expected, WitchSoundRules.Volume(distance, 5f, 30f));
        [TestCase(0f, 0.3f)] [TestCase(5f, 0.3f)] [TestCase(10f, 0.15f)]
        [TestCase(14f, 0.03f)] [TestCase(15f, 0f)] [TestCase(30f, 0f)]
        public void StepsMatchTiffanyWalkingVolumeAndFadeOutAtFifteenMeters(float distance, float expected)
        {
            rules.Tick(1f, new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Stalking, Speed = 1.2f }, distance);
            var sounds = new FootstepSounds(new[] { 4, 4, 4, 4, 4, 4 });
            float walkingVolume = sounds.Choose(GroundType.Dirt, Gait.Walk, 0f).Volume;
            Assert.AreEqual(expected, walkingVolume * rules.StepAttenuation, 1e-6f);
            Assert.AreEqual(WitchSoundRules.Volume(distance, 5f, 30f), rules.OneShotVolume);
        }
        [TestCase(WitchState.Landing, WitchState.Stalking)] [TestCase(WitchState.Landing, WitchState.Recovering)]
        [TestCase(WitchState.Landing, WitchState.WindUp)]
        [TestCase(WitchState.Stalking, WitchState.WindUp)]
        [TestCase(WitchState.WindUp, WitchState.Sprinting)]
        [TestCase(WitchState.Sprinting, WitchState.Caught)]
        public void TransitionsFireOnceAndNeverOnFirstTick(WitchState from, WitchState to)
        {
            Tick(to);
            Assert.IsFalse(HasEvent);
            Tick(from);
            Tick(to);
            Assert.AreEqual((from == WitchState.Landing, to == WitchState.WindUp, to == WitchState.Sprinting, to == WitchState.Caught),
                (rules.Landed, rules.Crack, rules.Whoosh, rules.Cackle));
            Tick(to);
            Assert.IsFalse(HasEvent);
        }
        [TestCase(1.2f)] [TestCase(2.5f)]
        public void StepsUseIrregularIntervalsAndStopAtZeroSpeed(float speed)
        {
            var decision = new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Stalking };
            rules.Tick(100f, decision, 0f);
            Assert.IsFalse(rules.Step);
            decision.Speed = speed;
            float[] factors = { 0.85f, 1.2f, 0.95f, 1.1f };
            for (int i = 0; i < 8; i++)
            {
                float interval = WitchSoundRules.StepInterval * WitchSoundRules.StepReferenceSpeed / speed * factors[i % 4];
                rules.Tick(interval / 2f, decision, 0f);
                Assert.IsFalse(rules.Step);
                rules.Tick(interval / 2f, decision, 0f);
                Assert.IsTrue(rules.Step);
            }
            decision.Speed = 0f;
            rules.Tick(100f, decision, 0f);
            Assert.IsFalse(rules.Step);
        }
        [Test]
        public void CacklesAlternateRespectCooldownAndAlwaysFireWhenCaught()
        {
            Tick(WitchState.Stalking);
            CackleAt(WitchState.Recovering, false);
            CackleAt(WitchState.TakingOff, true, 0);
            CackleAt(WitchState.Caught, true, 1);
            CackleAt(WitchState.Caught, false);
            CackleAt(WitchState.Recovering, false);
            CackleAt(WitchState.TakingOff, false);
            Tick(WitchState.Stalking, WitchSoundRules.CackleCooldown - 5f);
            CackleAt(WitchState.Recovering, false);
            CackleAt(WitchState.TakingOff, true, 2);
            CackleAt(WitchState.Caught, true, 0);
        }
        private void CackleAt(WitchState state, bool expected, int clip = 0)
        {
            Tick(state);
            Assert.AreEqual(expected, rules.Cackle);
            if (expected) Assert.AreEqual(clip, rules.CackleClip);
        }
    }
}
