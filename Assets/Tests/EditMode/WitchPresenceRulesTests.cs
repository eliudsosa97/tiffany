using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class WitchPresenceRulesTests
    {
        private WitchPresenceRules rules;
        [SetUp] public void SetUp() => rules = new WitchPresenceRules();
        private void Tick(float dt, WitchForm form = WitchForm.Grounded, WitchState state = WitchState.Stalking,
            float distance = 14f, bool onScreen = false) => rules.Tick(dt, form, state, distance, onScreen);
        [Test] public void HiddenNearbyGroundStalkingFadesInOverSixTenths()
        {
            Assert.IsFalse(rules.Audible);
            Tick(WitchPresenceRules.FadeIn * 0.5f);
            Assert.AreEqual(0.5f, rules.Volume, 1e-5f);
            Assert.IsTrue(rules.Audible);
            Tick(WitchPresenceRules.FadeIn * 0.5f);
            Assert.AreEqual(1f, rules.Volume);
            Tick(100f);
            Assert.AreEqual(1f, rules.Volume);
        }
        [TestCase(WitchForm.Flying, WitchState.Stalking, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Stalking, 14f, true)]
        [TestCase(WitchForm.Grounded, WitchState.Stalking, 20f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Stalking, 50f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Flying, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Landing, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.WindUp, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Sprinting, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Recovering, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.TakingOff, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Stunned, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Caught, 14f, false)]
        [TestCase(WitchForm.Grounded, WitchState.Suspicious, 14f, false)]
        public void LosingAnyConditionFadesOutOverOneSecond(WitchForm form, WitchState state, float distance, bool onScreen)
        {
            Tick(10f, form, state, distance, onScreen);
            Assert.AreEqual(0f, rules.Volume);
            Assert.IsFalse(rules.Audible);
            Tick(WitchPresenceRules.FadeIn);
            Tick(WitchPresenceRules.FadeOut * 0.5f, form, state, distance, onScreen);
            Assert.AreEqual(0.5f, rules.Volume, 1e-5f);
            Assert.IsTrue(rules.Audible);
            Tick(WitchPresenceRules.FadeOut * 0.5f, form, state, distance, onScreen);
            Assert.AreEqual(0f, rules.Volume);
            Assert.IsFalse(rules.Audible);
            Tick(100f, form, state, distance, onScreen);
            Assert.AreEqual(0f, rules.Volume);
        }
        [Test] public void DistanceJustUnderTwentyMetersIsNear()
        {
            Tick(WitchPresenceRules.FadeIn, distance: WitchPresenceRules.NearDistance - 0.01f);
            Assert.AreEqual(1f, rules.Volume);
        }
        [Test] public void ReturningConditionsReverseFadeFromCurrentVolume()
        {
            Tick(WitchPresenceRules.FadeIn);
            Tick(WitchPresenceRules.FadeOut * 0.5f, onScreen: true);
            Tick(WitchPresenceRules.FadeIn * 0.25f);
            Assert.AreEqual(0.75f, rules.Volume, 1e-5f);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaDoesNotAdvanceEitherFade(float dt)
        {
            Tick(dt);
            Assert.AreEqual(0f, rules.Volume);
            Assert.IsFalse(rules.Audible);
            Tick(WitchPresenceRules.FadeIn * 0.5f);
            Tick(dt, onScreen: true);
            Assert.AreEqual(0.5f, rules.Volume, 1e-5f);
            Assert.IsTrue(rules.Audible);
        }
    }
}
