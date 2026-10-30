using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class TensionMusicRulesTests
    {
        private TensionMusicRules rules;
        [SetUp] public void SetUp() => rules = new TensionMusicRules();
        [TestCase(WitchForm.Flying, WitchState.Flying, false)]
        [TestCase(WitchForm.Flying, WitchState.Landing, false)]
        [TestCase(WitchForm.Flying, WitchState.TakingOff, false)]
        [TestCase(WitchForm.Flying, WitchState.WindUp, false)]
        [TestCase(WitchForm.Grounded, WitchState.Landing, false)]
        [TestCase(WitchForm.Grounded, WitchState.TakingOff, false)]
        [TestCase(WitchForm.Grounded, WitchState.WindUp, false)]
        [TestCase(WitchForm.Grounded, WitchState.Stalking, true)]
        [TestCase(WitchForm.Grounded, WitchState.Sprinting, true)]
        [TestCase(WitchForm.Grounded, WitchState.Caught, false)]
        public void InactiveStatesAndHidingSilenceBothLayers(WitchForm form, WitchState state, bool hidden)
        {
            rules.Tick(2f, new WitchDecision { Form = form, State = state, Speed = 9f }, 0f, hidden, 2.5f);
            Assert.AreEqual((0f, 0f, 0f, 0f), (rules.DroneTarget, rules.PulseTarget, rules.Drone, rules.Pulse));
        }
        [TestCase(WitchState.Stalking, 0f, 2.5f, 0f)]
        [TestCase(WitchState.Stalking, 1.2f, 2.5f, 0f)]
        [TestCase(WitchState.Stalking, 2.48f, 2.5f, 0f)]
        [TestCase(WitchState.Stalking, 2.49f, 2.5f, 1f)]
        [TestCase(WitchState.Stalking, 2.5f, 2.5f, 1f)]
        [TestCase(WitchState.Stalking, 2.5f, 4f, 0f)]
        [TestCase(WitchState.Stalking, 4f, 4f, 1f)]
        [TestCase(WitchState.Sprinting, 9f, 2.5f, 1f)]
        [TestCase(WitchState.Recovering, 0f, 2.5f, 1f)]
        [TestCase(WitchState.Stunned, 9f, 2.5f, 0f)]
        public void GroundStatesAndProfileSpeedSelectLayers(WitchState state, float speed, float followSpeed, float pulse)
        {
            rules.Tick(1.5f, new WitchDecision { Form = WitchForm.Grounded, State = state, Speed = speed }, 0f, false, followSpeed);
            Assert.AreEqual((1f, pulse, 1f, pulse, 1f), (rules.DroneTarget, rules.PulseTarget, rules.Drone, rules.Pulse, rules.Level));
        }
        [TestCase(0f, 1f)] [TestCase(15f, 1f)] [TestCase(27.5f, 0.7f)]
        [TestCase(40f, 0.4f)] [TestCase(100f, 0.4f)] [TestCase(float.PositiveInfinity, 0.4f)]
        public void DistanceKeepsFarStalkingAudible(float distance, float expected)
        {
            rules.Tick(1.5f, new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Sprinting }, distance, false, 2.5f);
            Assert.AreEqual(expected, rules.DroneTarget, 1e-5f);
            Assert.AreEqual(expected, rules.PulseTarget, 1e-5f);
        }
        [Test]
        public void LayersFadeInOverOneAndAHalfSecondsAndOutOverTwo()
        {
            var decision = new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Sprinting };
            rules.Tick(0.75f, decision, 0f, false, 2.5f);
            Assert.AreEqual((0.5f, 0.5f, 0.5f), (rules.Drone, rules.Pulse, rules.Level));
            rules.Tick(0.75f, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 1f), (rules.Drone, rules.Pulse));
            rules.Tick(1f, decision, 0f, true, 2.5f);
            Assert.AreEqual((0.5f, 0.5f), (rules.Drone, rules.Pulse));
            rules.Tick(1f, decision, 0f, true, 2.5f);
            Assert.AreEqual((0f, 0f, 0f), (rules.Drone, rules.Pulse, rules.Level));
        }
        [TestCase(false)] [TestCase(true)]
        public void GroundedWindUpSilencesBothFullLayersOverThreeTenths(bool hidden)
        {
            var decision = new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Sprinting };
            rules.Tick(TensionMusicRules.FadeIn, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 1f, 0f), (rules.Drone, rules.Pulse, rules.Silence));
            decision.State = WitchState.WindUp;
            rules.Tick(0.15f, decision, 0f, hidden, 2.5f);
            Assert.AreEqual((0f, 0f), (rules.DroneTarget, rules.PulseTarget));
            Assert.AreEqual((0.5f, 0.5f, 0.5f), (rules.Drone, rules.Pulse, rules.Silence));
            rules.Tick(0.15f, decision, 0f, hidden, 2.5f);
            Assert.AreEqual((0f, 0f, 1f), (rules.Drone, rules.Pulse, rules.Silence));
        }
        [Test]
        public void SprintingRestoresBothLayersAndClearsSilenceOverNormalFadeIn()
        {
            var decision = new WitchDecision { Form = WitchForm.Grounded, State = WitchState.WindUp };
            rules.Tick(0.3f, decision, 0f, false, 2.5f);
            Assert.AreEqual((0f, 0f, 1f), (rules.Drone, rules.Pulse, rules.Silence));
            decision.State = WitchState.Sprinting;
            rules.Tick(0.75f, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 1f), (rules.DroneTarget, rules.PulseTarget));
            Assert.AreEqual((0.5f, 0.5f, 0.5f), (rules.Drone, rules.Pulse, rules.Silence));
            rules.Tick(0.75f, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 1f, 0f), (rules.Drone, rules.Pulse, rules.Silence));
        }
        [Test]
        public void FlyingWindUpDoesNotSilenceAmbience()
        {
            rules.Tick(0.3f, new WitchDecision { Form = WitchForm.Flying, State = WitchState.WindUp }, 0f, false, 2.5f);
            Assert.AreEqual(0f, rules.Silence);
        }
        [TestCase(0f, 1f)] [TestCase(1f, 0.55f)]
        public void AmbienceDucksWithMusic(float level, float expected) => Assert.AreEqual(expected, TensionMusicRules.AmbienceDuck(level));
        [Test]
        public void LeavingPursuitFadesOnlyPulse()
        {
            var decision = new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Sprinting };
            rules.Tick(1.5f, decision, 0f, false, 2.5f);
            decision.State = WitchState.Stalking;
            rules.Tick(1f, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 0f, 1f, 0.5f, 1f), (rules.DroneTarget, rules.PulseTarget, rules.Drone, rules.Pulse, rules.Level));
            rules.Tick(1f, decision, 0f, false, 2.5f);
            Assert.AreEqual((1f, 0f, 1f), (rules.Drone, rules.Pulse, rules.Level));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaDoesNotAdvanceFades(float dt)
        {
            rules.Tick(dt, new WitchDecision { Form = WitchForm.Grounded, State = WitchState.WindUp }, 0f, false, 2.5f);
            Assert.AreEqual((0f, 0f, 0f), (rules.Drone, rules.Pulse, rules.Silence));
        }
    }
}
