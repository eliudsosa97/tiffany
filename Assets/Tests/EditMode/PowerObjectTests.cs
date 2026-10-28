using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PowerObjectTests
    {
        [TestCase(false, 100f, CandleKind.Kindled, false, false, 100f)]
        [TestCase(true, 19f, CandleKind.Kindled, false, false, 19f)]
        [TestCase(true, 100f, CandleKind.Kindled, false, true, 80f)]
        [TestCase(true, 20f, CandleKind.Kindled, false, true, 0f)]
        [TestCase(true, 100f, CandleKind.Kindled, true, false, 100f)]
        [TestCase(true, 100f, CandleKind.Family, false, false, 100f)]
        [TestCase(true, 100f, (CandleKind)99, false, false, 100f)]
        public void LightingSpendsOnlyForUnlockedUnlitKindledCandle(bool unlocked, float start,
            CandleKind kind, bool lit, bool used, float remaining)
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy(start);
            var candle = new Candle(kind, Vector3.zero, lit);
            bool wasLit = candle.IsLit;
            if (unlocked)
            {
                powers.Unlock(Power.Candles);
            }
            Assert.AreEqual(used, PowerRules.TryLightCandle(powers, energy, candle));
            Assert.AreEqual(remaining, energy.Current);
            Assert.AreEqual(wasLit || used, candle.IsLit);
        }

        [TestCase(false, 100f, 12f, true, false, 100f)]
        [TestCase(true, 100f, 12.1f, true, false, 100f)]
        [TestCase(true, 100f, 12f, true, true, 85f)]
        [TestCase(true, 100f, 12f, false, false, 100f)]
        [TestCase(true, 14f, 12f, true, false, 14f)]
        [TestCase(true, 15f, 12f, true, true, 0f)]
        [TestCase(true, 100f, 0f, true, true, 85f)]
        [TestCase(true, 100f, -1f, true, false, 100f)]
        [TestCase(true, 100f, float.NaN, true, false, 100f)]
        [TestCase(true, 100f, float.PositiveInfinity, true, false, 100f)]
        public void MovingRequiresPowerEnergyRangeAndLight(bool unlocked, float start,
            float distance, bool visible, bool used, float remaining)
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy(start);
            var target = new Throwable(ThrowableKind.Bottle);
            if (unlocked)
            {
                powers.Unlock(Power.Poltergeist);
            }
            Assert.AreEqual(used, PowerRules.TryMove(powers, energy, target,
                distance, visible, false, out float radius));
            Assert.AreEqual(remaining, energy.Current);
            Assert.AreEqual(used, target.Used);
            Assert.AreEqual(used ? 10f : 0f, radius);
        }

        [TestCase(ThrowableKind.Bottle, 10f)]
        [TestCase(ThrowableKind.Can, 8f)]
        [TestCase(ThrowableKind.Pot, 12f)]
        [TestCase(ThrowableKind.Bell, 16f)]
        [TestCase(ThrowableKind.Candle, 6f)]
        public void EachKindMovesOnceAndRainHalvesItsNoise(ThrowableKind kind, float expectedRadius)
        {
            var powers = new PowerSet();
            powers.Unlock(Power.Poltergeist);
            var energy = new SoulEnergy();
            var target = new Throwable(kind);
            Assert.AreEqual(kind, target.Kind);
            Assert.IsFalse(target.Used);
            Assert.AreEqual(expectedRadius, Throwable.NoiseRadius(kind));
            Assert.IsTrue(PowerRules.TryMove(powers, energy, target, 12f, true, false, out float radius));
            Assert.AreEqual(expectedRadius, radius);
            Assert.AreEqual(85f, energy.Current);
            Assert.IsTrue(target.Used);
            Assert.IsFalse(PowerRules.TryMove(powers, energy, target, 12f, true, true, out radius));
            Assert.AreEqual(0f, radius);
            Assert.AreEqual(85f, energy.Current);
            Assert.IsTrue(target.Used);
            Assert.IsTrue(PowerRules.TryMove(powers, energy, new Throwable(kind), 12f, true, true, out radius));
            Assert.AreEqual(expectedRadius * 0.5f, radius);
            Assert.AreEqual(70f, energy.Current);
        }

        [TestCase(-1)]
        [TestCase(5)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidThrowableKindThrows(int value)
        {
            var kind = (ThrowableKind)value;
            Assert.Throws<ArgumentOutOfRangeException>(() => new Throwable(kind));
            Assert.Throws<ArgumentOutOfRangeException>(() => Throwable.NoiseRadius(kind));
        }

        [Test]
        public void NullArgumentsThrowEvenWhenPowersAreLocked()
        {
            var powers = new PowerSet();
            var energy = new SoulEnergy();
            var candle = new Candle(CandleKind.Kindled, Vector3.zero);
            var target = new Throwable(ThrowableKind.Bottle);
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryLightCandle(null, energy, candle));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryLightCandle(powers, null, candle));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryLightCandle(powers, energy, null));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryMove(null, energy, target, 12f, true, false, out _));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryMove(powers, null, target, 12f, true, false, out _));
            Assert.Throws<ArgumentNullException>(() => PowerRules.TryMove(powers, energy, null, 12f, true, false, out _));
            Assert.AreEqual(100f, energy.Current);
            Assert.IsFalse(candle.IsLit);
            Assert.IsFalse(target.Used);
        }
    }
}
