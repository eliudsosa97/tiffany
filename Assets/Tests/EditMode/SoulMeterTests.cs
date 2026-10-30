using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class SoulMeterTests
    {
        [Test]
        public void StartsHiddenUntilLightIsGrantedByAMemory()
        {
            var meter = new SoulMeter(new SoulEnergy());
            Assert.That(meter.IsUnlocked, Is.False);
            meter.Tick(20f);
            Assert.That(meter.IsUnlocked, Is.False);
            meter.Unlock(PowerSet.ForMemory(1));
            Assert.That(meter.IsUnlocked, Is.True);
        }

        [TestCase(Power.Candles)]
        [TestCase(Power.Poltergeist)]
        [TestCase(Power.Vanish)]
        [TestCase(Power.Manifest)]
        public void OtherPowersDoNotRevealTheMeter(Power power)
        {
            var meter = new SoulMeter(new SoulEnergy());
            meter.Unlock(power);
            Assert.That(meter.IsUnlocked, Is.False);
        }

        [Test]
        public void RemainsUnlockedAfterSpendingEverythingAndRecharging()
        {
            var energy = new SoulEnergy();
            var meter = new SoulMeter(energy);
            meter.Unlock(Power.Light);
            energy.TrySpend(SoulEnergy.Max);
            meter.Tick(1f);
            Assert.That(meter.IsUnlocked, Is.True);
            energy.Recharge(1f);
            meter.Unlock(Power.Candles);
            meter.Tick(1f);
            Assert.That(meter.IsUnlocked, Is.True);
        }

        [TestCase(-10f, 0f, true)]
        [TestCase(0f, 0f, true)]
        [TestCase(20f, 0.2f, true)]
        [TestCase(21f, 0.21f, false)]
        [TestCase(100f, 1f, false)]
        [TestCase(110f, 1f, false)]
        public void FractionAndLowThresholdFollowTheSoul(float energy, float fraction, bool low)
        {
            var meter = new SoulMeter(new SoulEnergy(energy));
            Assert.That(meter.Fraction, Is.EqualTo(fraction).Within(0.0001f));
            Assert.That(meter.IsLow, Is.EqualTo(low));
            Assert.That(meter.Change, Is.Zero);
        }

        [Test]
        public void UsesTheSameEnergyForSpendingAndFamilyCandleRecharge()
        {
            var energy = new SoulEnergy();
            var meter = new SoulMeter(energy);
            Assert.That(energy.TrySpend(80f), Is.True);
            meter.Tick(0f);
            Assert.That(meter.Fraction, Is.EqualTo(0.2f));
            Assert.That(meter.IsLow, Is.True);
            Assert.That(meter.Change, Is.EqualTo(-1));
            energy.Recharge(1f);
            meter.Tick(0f);
            Assert.That(meter.Fraction, Is.EqualTo(0.4f));
            Assert.That(meter.IsLow, Is.False);
            Assert.That(meter.Change, Is.EqualTo(1));
            Assert.That(energy.Current, Is.EqualTo(40f));
        }

        [Test]
        public void ChangeLingersThenClearsWithoutExtendingDuringPause()
        {
            var energy = new SoulEnergy();
            var meter = new SoulMeter(energy);
            energy.TrySpend(15f);
            meter.Tick(0f);
            meter.Tick(0f);
            meter.Tick(-1f);
            Assert.That(meter.Change, Is.EqualTo(-1));
            meter.Tick(SoulMeter.ChangeSeconds);
            Assert.That(meter.Change, Is.Zero);
        }

        [Test]
        public void PassiveLightDrainUpdatesFillWithoutFlashingLikeAPower()
        {
            var energy = new SoulEnergy();
            var meter = new SoulMeter(energy);
            energy.Drain(0.02f);
            meter.Tick(0.02f);
            Assert.That(meter.Fraction, Is.LessThan(1f));
            Assert.That(meter.Change, Is.Zero);
        }
    }
}
