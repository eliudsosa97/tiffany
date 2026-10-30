using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class FlashChargeTests
    {
        [TestCase(0.2f, 0f)]
        [TestCase(0.3f, 0f)]
        [TestCase(1.65f, 0.5f)]
        [TestCase(3f, 1f)]
        [TestCase(10f, 1f)]
        public void ProgressFillsBetweenTapThresholdAndThreeSeconds(float seconds, float expected)
        {
            var charge = new FlashCharge();
            charge.Tick(seconds, true, true);
            Assert.That(charge.Progress, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(0.2f, 100f, false)]
        [TestCase(0.3f, 100f, true)]
        [TestCase(1.65f, 50f, true)]
        [TestCase(1.65f, 49f, true)]
        [TestCase(1.65f, 0f, true)]
        public void IndicatorShowsHoldAndEnergyWithoutEnablingUnavailableBurst(float seconds, float energy, bool visible)
        {
            var charge = new FlashCharge();
            charge.Tick(seconds, true, energy >= SoulCosts.Flash);
            var indicator = new FlashChargeIndicator();
            indicator.Tick(0f, charge, energy, InterfaceScreen.Playing, false);
            Assert.AreEqual(visible, indicator.Visible);
            Assert.AreEqual(energy >= 50f ? FlashChargeIndicator.Warm : FlashChargeIndicator.LowEnergy, indicator.Tint);
            Assert.That(indicator.Fill, Is.EqualTo(charge.AttemptProgress).Within(0.0001f));
            charge.Tick(0.01f, false, true);
            indicator.Tick(0.01f, charge, energy, InterfaceScreen.Playing, false);
            Assert.IsFalse(indicator.Visible);
            Assert.AreEqual(0f, indicator.Fill);
        }

        [TestCase(InterfaceScreen.Title, false)]
        [TestCase(InterfaceScreen.Paused, false)]
        [TestCase(InterfaceScreen.Notebook, false)]
        [TestCase(InterfaceScreen.Story, false)]
        [TestCase(InterfaceScreen.Ending, false)]
        [TestCase(InterfaceScreen.Playing, true)]
        public void IndicatorIsHiddenOutsideGameplayOrDuringEcho(InterfaceScreen screen, bool blocked)
        {
            var charge = new FlashCharge();
            charge.Tick(1f, true, true);
            var indicator = new FlashChargeIndicator();
            indicator.Tick(0f, charge, 100f, screen, blocked);
            Assert.IsFalse(indicator.Visible);
            charge.Tick(2f, true, true);
            indicator.Tick(0f, charge, 50f, screen, blocked);
            Assert.IsFalse(indicator.Visible);
        }

        [Test]
        public void CompletedIndicatorExpandsAndFadesOnceEvenAfterEnergyWasSpent()
        {
            var charge = new FlashCharge();
            charge.Tick(3f, true, true);
            var indicator = new FlashChargeIndicator();
            indicator.Tick(0f, charge, 0f, InterfaceScreen.Playing, false);
            Assert.IsTrue(indicator.Visible);
            Assert.AreEqual(1f, indicator.Fill);
            Assert.AreEqual(1.3f, indicator.Scale);
            Assert.AreEqual(FlashChargeIndicator.Warm, indicator.Tint);
            charge.Tick(0.125f, true, true);
            indicator.Tick(0.125f, charge, 0f, InterfaceScreen.Playing, false);
            Assert.AreEqual(0.5f, indicator.Tint.a);
            indicator.Tick(0.125f, charge, 0f, InterfaceScreen.Playing, false);
            Assert.IsFalse(indicator.Visible);
            Assert.AreEqual(1f, indicator.Scale);
        }

        [Test]
        public void ShortTapTogglesOnlyOnReleaseWithoutCharging()
        {
            var charge = new FlashCharge();
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.2f, true, true));
            Assert.IsFalse(charge.Charging);
            Assert.AreEqual(0f, charge.Progress);
            Assert.AreEqual(0f, charge.FloatOffset);
            Assert.AreEqual(FlashChargeResult.Toggle, charge.Tick(0.01f, false, true));
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.01f, false, true));
        }

        [Test]
        public void ReleasingPartialChargeCancelsAndSettlesWithoutBurst()
        {
            var charge = new FlashCharge();
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(1.5f, true, true));
            Assert.IsTrue(charge.Charging);
            Assert.AreEqual(FlashCharge.ChargeMoveScale, charge.MoveScale);
            Assert.That(charge.Progress, Is.InRange(0f, 1f));
            float height = charge.FloatOffset;
            Assert.Greater(height, 0f);
            Assert.AreEqual(FlashChargeResult.Cancelled, charge.Tick(0.01f, false, true));
            Assert.IsFalse(charge.Charging);
            Assert.AreEqual(1f, charge.MoveScale);
            Assert.That(charge.FloatOffset, Is.GreaterThan(0f).And.LessThan(height));
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(FlashCharge.PulseFade, false, true));
            Assert.AreEqual(0f, charge.FloatOffset);
        }

        [Test]
        public void CrossingThreeSecondsBurstsOnceUntilReleased()
        {
            var charge = new FlashCharge();
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(2.9f, true, true));
            Assert.AreEqual(FlashChargeResult.Burst, charge.Tick(0.1f, true, true));
            Assert.IsFalse(charge.Charging);
            Assert.AreEqual(1f, charge.MoveScale);
            Assert.AreEqual(FlashCharge.FloatHeight, charge.FloatOffset);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(4f, true, true));
            Assert.AreEqual(0f, charge.FloatOffset);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.1f, false, true));
            Assert.AreEqual(FlashChargeResult.Burst, charge.Tick(3f, true, true));
        }

        [Test]
        public void UnavailableChargeStillAllowsTapButNeverChargesOrBursts()
        {
            var charge = new FlashCharge();
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.2f, true, false));
            Assert.AreEqual(FlashChargeResult.Toggle, charge.Tick(0.01f, false, false));
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(4f, true, false));
            Assert.IsFalse(charge.Charging);
            Assert.AreEqual(0f, charge.Progress);
            Assert.AreEqual(0f, charge.FloatOffset);
            Assert.AreEqual(1f, charge.MoveScale);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.01f, false, false));
        }

        [Test]
        public void LosingEligibilityCancelsTheHoldUntilReleased()
        {
            var charge = new FlashCharge();
            charge.Tick(1f, true, true);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(1f, true, false));
            Assert.IsFalse(charge.Charging);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(4f, true, true));
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.1f, false, true));
            Assert.AreEqual(FlashChargeResult.Burst, charge.Tick(3f, true, true));
        }

        [TestCase(10.9f, 0f, true)]
        [TestCase(11.1f, 0f, false)]
        [TestCase(10.9f, 100f, true)]
        [TestCase(11f, -100f, true)]
        [TestCase(11.1f, -100f, false)]
        public void BurstUsesHorizontalRadius(float distance, float height, bool inside)
        {
            var player = new Vector3(2f, 4f, -3f);
            var witch = player + new Vector3(0f, height, -distance);
            Assert.AreEqual(inside, FlashCharge.InBurst(player, witch, FlashCharge.BurstRadius));
        }

        [Test]
        public void FloatingRisesSmoothlyToHeightWithoutOvershooting()
        {
            var charge = new FlashCharge();
            float previous = 0f;
            for (int i = 0; i < 12; i++)
            {
                var result = charge.Tick(0.25f, true, true);
                Assert.AreEqual(i == 11 ? FlashChargeResult.Burst : FlashChargeResult.None, result);
                Assert.That(charge.FloatOffset, Is.InRange(previous, FlashCharge.FloatHeight));
                Assert.That(charge.Progress, Is.InRange(0f, 1f));
                previous = charge.FloatOffset;
            }
            Assert.AreEqual(FlashCharge.FloatHeight, charge.FloatOffset);
            charge.Tick(0.1f, true, true);
            Assert.That(charge.FloatOffset, Is.GreaterThan(0f).And.LessThan(FlashCharge.FloatHeight));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaDoesNotAdvanceOrReleaseCharge(float dt)
        {
            var charge = new FlashCharge();
            charge.Tick(1f, true, true);
            float progress = charge.Progress;
            float height = charge.FloatOffset;
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(dt, false, false));
            Assert.AreEqual(progress, charge.Progress);
            Assert.AreEqual(height, charge.FloatOffset);
            Assert.IsTrue(charge.Charging);
            Assert.AreEqual(FlashChargeResult.Burst, charge.Tick(2f, true, true));
        }
    }
}
