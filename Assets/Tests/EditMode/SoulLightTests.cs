using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class SoulLightTests
    {
        private PowerSet powers;
        private SoulEnergy energy;
        private SoulLight light;

        [SetUp]
        public void SetUp()
        {
            powers = new PowerSet();
            energy = new SoulEnergy();
            light = new SoulLight(powers, energy);
        }

        [Test]
        public void ConstructorRejectsNullDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new SoulLight(null, energy));
            Assert.Throws<ArgumentNullException>(() => new SoulLight(powers, null));
        }

        [Test]
        public void ToggleNeedsLightPowerAndEnergyAndCanTurnOff()
        {
            Assert.IsFalse(light.Toggle());
            Assert.IsFalse(light.Visible);
            powers.Unlock(Power.Light);
            Assert.IsTrue(light.Toggle());
            Assert.IsTrue(light.IsOn);
            Assert.IsTrue(light.Visible);
            Assert.IsFalse(light.Toggle());
            Assert.IsFalse(light.Visible);
            energy.ResetTo(0f);
            Assert.IsFalse(light.Toggle());
        }

        [Test]
        public void TickDrainsOnlyWhileOnAndEmptyEnergyTurnsLightOff()
        {
            powers.Unlock(Power.Light);
            light.Tick(2f);
            Assert.AreEqual(SoulEnergy.Max, energy.Current);
            light.Toggle();
            light.Tick(2f);
            Assert.AreEqual(98f, energy.Current);
            light.Toggle();
            light.Tick(2f);
            Assert.AreEqual(98f, energy.Current);
            light.Toggle();
            light.Tick(100f);
            Assert.AreEqual(0f, energy.Current);
            Assert.IsFalse(light.IsOn);
            Assert.IsFalse(light.Visible);
            Assert.IsFalse(light.Toggle());
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void InvalidTimeDoesNotDrainOrAdvanceFlash(float dt)
        {
            powers.Unlock(Power.Light);
            light.Toggle();
            Assert.IsTrue(light.TryFlash(false, false, true, out _));
            light.Tick(dt);
            Assert.AreEqual(50f, energy.Current);
            Assert.AreEqual(SoulLight.FlashDuration, light.FlashRemaining);
            Assert.IsTrue(light.IsOn);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlashSpendsFiftyAndExpiresWithoutChangingSwitch(bool on)
        {
            powers.Unlock(Power.Light);
            if (on) light.Toggle();
            Assert.IsTrue(light.TryFlash(false, false, true, out bool stuns));
            Assert.IsFalse(stuns);
            Assert.AreEqual(50f, energy.Current);
            Assert.AreEqual(on, light.IsOn);
            Assert.IsTrue(light.IsFlashing);
            Assert.IsTrue(light.Visible);
            Assert.AreEqual(SoulLight.FlashDuration, light.FlashRemaining);
            light.Tick(SoulLight.FlashDuration * 0.5f);
            Assert.IsTrue(light.IsFlashing);
            light.Tick(SoulLight.FlashDuration * 0.5f);
            Assert.AreEqual(0f, light.FlashRemaining);
            Assert.IsFalse(light.IsFlashing);
            Assert.AreEqual(on, light.IsOn);
            Assert.AreEqual(on, light.Visible);
            light.Tick(1f);
            Assert.AreEqual(0f, light.FlashRemaining);
        }

        [TestCase(false, 100f)]
        [TestCase(true, 49f)]
        [TestCase(true, 0f)]
        public void FailedFlashDoesNotSpendOrShowLight(bool unlocked, float start)
        {
            if (unlocked) powers.Unlock(Power.Light);
            energy.ResetTo(start);
            Assert.IsFalse(light.TryFlash(true, true, true, out bool stuns));
            Assert.IsFalse(stuns);
            Assert.AreEqual(start, energy.Current);
            Assert.IsFalse(light.IsOn);
            Assert.IsFalse(light.IsFlashing);
            Assert.IsFalse(light.Visible);
        }

        [TestCase(false, false, true, false)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, true, true)]
        [TestCase(true, true, false, false)]
        public void FlashStunsOnlyGroundedWitchInRangeWithLineOfSight(
            bool grounded, bool inRange, bool lineOfSight, bool expected)
        {
            powers.Unlock(Power.Light);
            energy.ResetTo(SoulCosts.Flash);
            Assert.IsTrue(light.TryFlash(grounded, inRange, lineOfSight, out bool stuns));
            Assert.AreEqual(expected, stuns);
            Assert.AreEqual(0f, energy.Current);
            Assert.IsTrue(light.Visible);
        }

        [Test]
        public void HidingExtinguishesLightAndFlashAndBlocksBothUntilLeaving()
        {
            powers.Unlock(Power.Light);
            light.Toggle();
            light.TryFlash(true, true, true, out _);
            float remaining = energy.Current;
            light.SetHidden(true);
            Assert.IsFalse(light.IsOn || light.IsFlashing || light.Visible);
            Assert.IsFalse(light.Toggle());
            Assert.IsFalse(light.TryFlash(true, true, true, out bool stuns));
            Assert.IsFalse(stuns);
            Assert.AreEqual(remaining, energy.Current);
            light.SetHidden(false);
            Assert.IsFalse(light.Visible);
            Assert.IsTrue(light.Toggle());
        }

        [TestCase(0f, false, false)]
        [TestCase(1f, false, true)]
        [TestCase(1f, true, false)]
        public void InputRequiresRunningTimeAndExposedPlayer(float timeScale, bool hidden, bool expected)
        {
            light.SetHidden(hidden);
            Assert.AreEqual(expected, light.CanReadInput(timeScale));
        }

        [TestCase(0f, 5f, 0f, true)]
        [TestCase(180f, 5f, 0f, false)]
        [TestCase(0f, 11f, 0f, true)]
        [TestCase(0f, 11.1f, 0f, false)]
        [TestCase(35f, 5f, 0f, true)]
        [TestCase(-35f, 5f, 0f, true)]
        [TestCase(36f, 5f, 0f, false)]
        [TestCase(-36f, 5f, 0f, false)]
        [TestCase(0f, 5f, 100f, true)]
        [TestCase(180f, 5f, 100f, false)]
        public void ConeChecksHorizontalRangeAndAngle(float angle, float distance, float height, bool expected)
        {
            var origin = new Vector3(7f, 2f, -3f);
            Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance + Vector3.up * height;
            Assert.AreEqual(expected, SoulLight.InCone(origin, new Vector3(0f, 10f, 1f), point, 11f, 35f));
        }

        [Test]
        public void ConeIncludesOriginButNeedsHorizontalForward()
        {
            Assert.IsTrue(SoulLight.InCone(Vector3.one, Vector3.forward, Vector3.one, 0f, 35f));
            Assert.IsFalse(SoulLight.InCone(Vector3.zero, Vector3.zero, Vector3.forward, 11f, 35f));
            Assert.IsFalse(SoulLight.InCone(Vector3.zero, Vector3.up, Vector3.forward, 11f, 35f));
        }

        [TestCase(true, false, false, 1f, 0f)]
        [TestCase(true, true, false, 1f, 0f)]
        [TestCase(true, true, true, 1f, 0f)]
        [TestCase(false, false, false, 1f, 1f)]
        [TestCase(false, false, true, 1f, 1f)]
        [TestCase(false, true, false, 1f, 2.2f)]
        [TestCase(false, true, false, 0.5f, 1.6f)]
        [TestCase(false, true, true, 1f, 5.8f)]
        public void BodyGlowHidesOrReturnsBaseOrBoostsWithSizeAndFlash(bool hidden, bool visible, bool flashing, float size, float expected) =>
            Assert.AreEqual(expected, SoulLight.BodyGlow(hidden, visible, flashing, size, 1.2f, 4f), 0.0001f);

        [Test]
        public void SizeGrowsLinearlyUpToLowEnergyThreshold()
        {
            Assert.AreEqual(SoulLight.MinSize, SoulLight.Size(0f));
            Assert.AreEqual(1f, SoulLight.Size(20f));
            Assert.AreEqual(1f, SoulLight.Size(100f));
            float previous = SoulLight.Size(0f);
            for (int value = 1; value <= 20; value++)
            {
                float size = SoulLight.Size(value);
                Assert.Greater(size, previous);
                Assert.AreEqual(SoulLight.MinSize + (1f - SoulLight.MinSize) * value / 20f, size, 1e-5f);
                previous = size;
            }
        }

        [Test]
        public void FlickerVariesOnlyBelowThresholdAndGrowsAsEnergyFalls()
        {
            float minimum = 1f;
            float maximum = SoulLight.MinFlicker;
            for (int i = 0; i < 30; i++)
            {
                float time = i * 0.037f;
                Assert.AreEqual(1f, SoulLight.Flicker(20f, time));
                Assert.AreEqual(1f, SoulLight.Flicker(100f, time));
                float value = SoulLight.Flicker(2f, time);
                Assert.That(value, Is.InRange(SoulLight.MinFlicker, 1f));
                Assert.LessOrEqual(value, SoulLight.Flicker(10f, time));
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }
            Assert.Greater(maximum - minimum, 0.01f);
        }

        [Test]
        public void PlayerSoulInitializesLazilyAndKeepsSharedInstances()
        {
            var player = new GameObject("PlayerSoulTest");
            try
            {
                var soul = player.AddComponent<PlayerSoul>();
                soul.startEnergy = 37f;
                soul.startingPowers = new[] { Power.Light, Power.Candles };
                var storedEnergy = soul.Energy;
                var storedPowers = soul.Powers;
                Assert.AreEqual(37f, storedEnergy.Current);
                Assert.IsTrue(storedPowers.Has(Power.Light));
                Assert.IsTrue(storedPowers.Has(Power.Candles));
                Assert.IsFalse(storedPowers.Has(Power.Vanish));
                Assert.AreEqual(2, storedPowers.Count);
                storedEnergy.TrySpend(7f);
                storedPowers.Unlock(Power.Vanish);
                Assert.AreSame(storedEnergy, soul.Energy);
                Assert.AreSame(storedPowers, soul.Powers);
                Assert.AreEqual(30f, soul.Energy.Current);
                Assert.IsTrue(soul.Powers.Has(Power.Vanish));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }
    }
}
