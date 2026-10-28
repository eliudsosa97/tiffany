using System;
using NUnit.Framework;

namespace ElVelador.Tests
{
    public sealed class PowerTests
    {
        [TestCase(1, Power.Light, "Light")]
        [TestCase(2, Power.Candles, "Candles")]
        [TestCase(3, Power.Poltergeist, "Poltergeist")]
        [TestCase(4, Power.Vanish, "Vanish")]
        [TestCase(5, Power.Manifest, "Manifest")]
        public void MemoriesAndNamesMapToPowersUnlockedOnlyOnce(int id, Power expected, string name)
        {
            var powers = new PowerSet();
            Assert.AreEqual(0, powers.Count);
            Assert.IsFalse(powers.Has(expected));
            Assert.AreEqual(expected, PowerSet.ForMemory(id));
            Assert.IsTrue(PowerSet.TryParse(name, out Power parsed));
            Assert.AreEqual(expected, parsed);
            powers.Unlock(expected);
            powers.Unlock(expected);
            Assert.IsTrue(powers.Has(expected));
            Assert.AreEqual(1, powers.Count);
        }

        [Test]
        public void CountIncludesEachUnlockedPowerOnce()
        {
            var powers = new PowerSet();
            for (int id = 1; id <= 5; id++)
            {
                Power power = PowerSet.ForMemory(id);
                powers.Unlock(power);
                powers.Unlock(power);
                Assert.AreEqual(id, powers.Count);
            }
        }

        [TestCase(-1)]
        [TestCase(5)]
        [TestCase(9)]
        [TestCase(32)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidPowerThrowsWithoutChangingUnlockedPowers(int value)
        {
            var powers = new PowerSet();
            powers.Unlock(Power.Light);
            var power = (Power)value;

            Assert.Throws<ArgumentOutOfRangeException>(() => powers.Has(power));
            Assert.Throws<ArgumentOutOfRangeException>(() => powers.Unlock(power));
            Assert.AreEqual(1, powers.Count);
            Assert.IsTrue(powers.Has(Power.Light));
        }

        [TestCase(0)]
        [TestCase(6)]
        public void InvalidMemoryThrows(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PowerSet.ForMemory(id));
        }

        [TestCase("light")]
        [TestCase("2")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase(" Light")]
        [TestCase("Light,Candles")]
        public void InvalidNameIsRejected(string name)
        {
            Assert.IsFalse(PowerSet.TryParse(name, out _));
        }

        [Test]
        public void LightNeedsUnlockAndNonemptyEnergyWithoutSpending()
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy();
            Assert.IsFalse(PowerRules.CanUseLight(powers, energy));
            powers.Unlock(Power.Light);
            Assert.IsTrue(PowerRules.CanUseLight(powers, energy));
            Assert.AreEqual(100f, energy.Current);
            energy.ResetTo(0f);
            Assert.IsFalse(PowerRules.CanUseLight(powers, energy));
        }

        [TestCase(false, 100f, true, true, false, 100f, false)]
        [TestCase(true, 49f, true, true, false, 49f, false)]
        [TestCase(true, 100f, false, false, true, 50f, false)]
        [TestCase(true, 100f, false, true, true, 50f, false)]
        [TestCase(true, 100f, true, false, true, 50f, false)]
        [TestCase(true, 100f, true, true, true, 50f, true)]
        [TestCase(true, 50f, true, true, true, 0f, true)]
        public void FlashSpendsOnlyWhenUsableAndStunsOnlyInGroundCone(bool unlocked, float start,
            bool grounded, bool inCone, bool used, float remaining, bool stuns)
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy(start);
            if (unlocked)
            {
                powers.Unlock(Power.Light);
            }
            Assert.AreEqual(used, PowerRules.TryFlash(powers, energy, grounded, inCone, out bool actualStun));
            Assert.AreEqual(stuns, actualStun);
            Assert.AreEqual(remaining, energy.Current);
        }

        [Test]
        public void VanishRequiresUnlockAndEnergyAndCannotRestartWhileActive()
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy();
            var vanish = new Vanish();
            Assert.IsFalse(vanish.IsActive);
            Assert.AreEqual(0f, vanish.Remaining);
            Assert.IsFalse(vanish.TryStart(powers, energy));
            Assert.AreEqual(100f, energy.Current);
            powers.Unlock(Power.Vanish);
            energy.ResetTo(29f);
            Assert.IsFalse(vanish.TryStart(powers, energy));
            Assert.AreEqual(29f, energy.Current);
            Assert.IsFalse(vanish.IsActive);
            energy.ResetTo(100f);
            Assert.IsTrue(vanish.TryStart(powers, energy));
            Assert.AreEqual(70f, energy.Current);
            Assert.IsTrue(vanish.IsActive);
            Assert.AreEqual(3f, vanish.Remaining);
            vanish.Tick(2.9f);
            Assert.IsTrue(vanish.IsActive);
            Assert.IsFalse(vanish.TryStart(powers, energy));
            Assert.AreEqual(70f, energy.Current);
            Assert.AreEqual(0.1f, vanish.Remaining, 1e-4f);
            vanish.Tick(0.2f);
            Assert.IsFalse(vanish.IsActive);
            Assert.AreEqual(0f, vanish.Remaining);
            energy.ResetTo(30f);
            Assert.IsTrue(vanish.TryStart(powers, energy));
            Assert.AreEqual(0f, energy.Current);
            Assert.AreEqual(3f, vanish.Remaining);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void InvalidTimeDoesNotChangeVanish(float dt)
        {
            var powers = new PowerSet();
            powers.Unlock(Power.Vanish);
            var vanish = new Vanish();
            vanish.Tick(dt);
            Assert.AreEqual(0f, vanish.Remaining);
            Assert.IsTrue(vanish.TryStart(powers, new SoulEnergy()));
            vanish.Tick(dt);
            Assert.AreEqual(3f, vanish.Remaining);
            Assert.IsTrue(vanish.IsActive);
        }

        [Test]
        public void NullDependenciesThrowEvenWhenPowersAreLocked()
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy();
            var vanish = new Vanish();
            Assert.Throws<ArgumentNullException>(() => PowerRules.CanUseLight(null, energy));
            Assert.Throws<ArgumentNullException>(() => PowerRules.CanUseLight(powers, null));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryFlash(null, energy, true, true, out _));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryFlash(powers, null, true, true, out _));
            Assert.Throws<ArgumentNullException>(() => vanish.TryStart(null, energy));
            Assert.Throws<ArgumentNullException>(() => vanish.TryStart(powers, null));
            Assert.AreEqual(100f, energy.Current);
        }
    }
}
