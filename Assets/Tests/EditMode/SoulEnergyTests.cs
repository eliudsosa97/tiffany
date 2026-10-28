using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class SoulEnergyTests
    {
        [Test]
        public void StartsWithFullEnergy()
        {
            var energy = new SoulEnergy();

            Assert.AreEqual(100f, energy.Current, 1e-4f);
            Assert.IsFalse(energy.IsEmpty);
        }

        [TestCase(-10f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(50f, 50f)]
        [TestCase(500f, 100f)]
        public void ConstructorClampsStartingEnergy(float start, float expected)
        {
            var energy = new SoulEnergy(start);

            Assert.AreEqual(expected, energy.Current, 1e-4f);
            Assert.AreEqual(expected == 0f, energy.IsEmpty);
        }

        [TestCase(10f, 90f)]
        [TestCase(1000f, 0f)]
        public void DrainConsumesLightEnergyWithoutGoingBelowZero(float dt, float expected)
        {
            var energy = new SoulEnergy();

            energy.Drain(dt);

            Assert.AreEqual(expected, energy.Current, 1e-4f);
            Assert.AreEqual(expected == 0f, energy.IsEmpty);
        }

        [TestCase(1f, 70f)]
        [TestCase(1000f, 100f)]
        public void RechargeRestoresEnergyWithoutExceedingMaximum(float dt, float expected)
        {
            var energy = new SoulEnergy(50f);

            energy.Recharge(dt);

            Assert.AreEqual(expected, energy.Current, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimeDoesNotChangeEnergy(float dt)
        {
            var energy = new SoulEnergy(50f);

            energy.Drain(dt);
            Assert.AreEqual(50f, energy.Current, 1e-4f);

            energy.Recharge(dt);
            Assert.AreEqual(50f, energy.Current, 1e-4f);
        }

        [TestCase(100f, SoulCosts.Flash, true, 50f)]
        [TestCase(40f, 50f, false, 40f)]
        [TestCase(0f, 15f, false, 0f)]
        [TestCase(15f, 15f, true, 0f)]
        [TestCase(0f, 0f, true, 0f)]
        public void TrySpendOnlyDeductsAffordableAmounts(float start, float amount, bool success, float expected)
        {
            var energy = new SoulEnergy(start);

            Assert.AreEqual(success, energy.TrySpend(amount));
            Assert.AreEqual(expected, energy.Current, 1e-4f);
            Assert.AreEqual(expected == 0f, energy.IsEmpty);
        }

        [Test]
        public void TrySpendRejectsNegativeAmountsWithoutChangingEnergy()
        {
            var energy = new SoulEnergy(50f);

            Assert.Throws<ArgumentOutOfRangeException>(() => energy.TrySpend(-1f));
            Assert.AreEqual(50f, energy.Current, 1e-4f);
        }

        [TestCase(-10f, 0f)]
        [TestCase(50f, 50f)]
        [TestCase(500f, 100f)]
        public void ResetToClampsEnergy(float value, float expected)
        {
            var energy = new SoulEnergy();

            energy.ResetTo(value);

            Assert.AreEqual(expected, energy.Current, 1e-4f);
            Assert.AreEqual(expected == 0f, energy.IsEmpty);
        }
    }
}
