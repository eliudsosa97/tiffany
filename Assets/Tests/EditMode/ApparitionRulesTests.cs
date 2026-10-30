using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class ApparitionRulesTests
    {
        private ApparitionRules rules;
        [SetUp] public void SetUp() => rules = new ApparitionRules();
        private ApparitionEvent Tick(float distance = 20f, bool lit = false, bool chasing = false, float witch = 30f, float dt = 0.1f) =>
            rules.Tick(dt, distance, lit, chasing, witch);
        [Test] public void StartsWaitingAndInvisible()
        {
            Assert.AreEqual(ApparitionState.Waiting, rules.State);
            Assert.AreEqual(0f, rules.Alpha);
        }
        [TestCase(12f)] [TestCase(20f)] [TestCase(25f)]
        public void AppearsImmediatelyWithinShowRange(float distance)
        {
            Assert.AreEqual(ApparitionEvent.Appeared, Tick(distance));
            Assert.AreEqual(ApparitionState.Shown, rules.State);
            Assert.AreEqual(1f, rules.Alpha);
            Assert.AreEqual(ApparitionEvent.None, Tick(distance));
        }
        [TestCase(10f)] [TestCase(26f)] [TestCase(11.9f)] [TestCase(25.1f)]
        public void WaitsOutsideShowRange(float distance)
        {
            Assert.AreEqual(ApparitionEvent.None, Tick(distance));
            Assert.AreEqual(ApparitionState.Waiting, rules.State);
            Assert.AreEqual(0f, rules.Alpha);
        }
        [TestCase(true, 30f)] [TestCase(false, 29f)]
        public void WaitsUntilChaseAndRealWitchAreClear(bool chasing, float witch)
        {
            Assert.AreEqual(ApparitionEvent.None, Tick(chasing: chasing, witch: witch));
            Assert.AreEqual(ApparitionState.Waiting, rules.State);
            Assert.AreEqual(ApparitionEvent.Appeared, Tick());
        }
        [TestCase(7.9f, false)] [TestCase(15f, true)]
        public void NearPlayerOrLightVanishesWithOneKnock(float distance, bool lit)
        {
            Tick();
            Assert.AreEqual(ApparitionEvent.VanishedWithKnock, Tick(distance, lit));
            Assert.AreEqual(ApparitionState.Vanishing, rules.State);
            Assert.AreEqual(1f, rules.Alpha);
            Assert.AreEqual(ApparitionEvent.None, Tick(distance, lit));
        }
        [TestCase(20f, true, 30f)] [TestCase(20f, false, 29f)] [TestCase(36f, false, 30f)]
        public void ChaseNearbyWitchOrLeavingVanishesQuietly(float distance, bool chasing, float witch)
        {
            Tick();
            Assert.AreEqual(ApparitionEvent.VanishedQuietly, Tick(distance, chasing: chasing, witch: witch));
            Assert.AreEqual(ApparitionState.Vanishing, rules.State);
        }
        [TestCase(true, 30f)] [TestCase(false, 29f)]
        public void ChaseOrNearbyWitchSuppressesKnockEvenWhenNearAndLit(bool chasing, float witch)
        {
            Tick();
            Assert.AreEqual(ApparitionEvent.VanishedQuietly, Tick(7f, true, chasing, witch));
        }
        [TestCase(8f)] [TestCase(35f)]
        public void ExactVanishThresholdsKeepFigureShown(float distance)
        {
            Tick();
            Assert.AreEqual(ApparitionEvent.None, Tick(distance));
            Assert.AreEqual(ApparitionState.Shown, rules.State);
            Assert.AreEqual(1f, rules.Alpha);
        }
        [Test] public void FadesLinearlyForThreeTenthsOfASecondThenStaysDone()
        {
            Tick();
            Tick(lit: true);
            Assert.AreEqual(ApparitionEvent.None, Tick(dt: 0.15f));
            Assert.AreEqual(ApparitionState.Vanishing, rules.State);
            Assert.AreEqual(0.5f, rules.Alpha, 1e-6f);
            Assert.AreEqual(ApparitionEvent.None, Tick(dt: 0.15f));
            Assert.AreEqual(ApparitionState.Done, rules.State);
            Assert.AreEqual(0f, rules.Alpha);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(ApparitionEvent.None, Tick());
                Assert.AreEqual(ApparitionState.Done, rules.State);
                Assert.AreEqual(0f, rules.Alpha);
            }
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaCannotAppearVanishOrAdvanceFade(float dt)
        {
            Assert.AreEqual(ApparitionEvent.None, Tick(dt: dt));
            Assert.AreEqual(ApparitionState.Waiting, rules.State);
            Assert.AreEqual(0f, rules.Alpha);
            Tick();
            Assert.AreEqual(ApparitionEvent.None, Tick(7f, true, true, 0f, dt));
            Assert.AreEqual(ApparitionState.Shown, rules.State);
            Assert.AreEqual(1f, rules.Alpha);
            Tick(lit: true);
            Tick(dt: 0.15f);
            Assert.AreEqual(ApparitionEvent.None, Tick(dt: dt));
            Assert.AreEqual(ApparitionState.Vanishing, rules.State);
            Assert.AreEqual(0.5f, rules.Alpha, 1e-6f);
            Tick(dt: 0.15f);
            Assert.AreEqual(ApparitionState.Done, rules.State);
        }
    }
}
