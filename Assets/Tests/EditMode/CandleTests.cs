using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class CandleTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void FamilyCandlesStayLitInAnyWeather(bool lit)
        {
            var candle = new Candle(CandleKind.Family, Vector3.one, lit);
            Assert.AreEqual(CandleKind.Family, candle.Kind);
            Assert.AreEqual(Vector3.one, candle.Position);
            Assert.IsTrue(candle.IsLit);
            Assert.IsTrue(candle.IsSafeZone);
            Assert.AreEqual(float.PositiveInfinity, candle.Remaining);
            Assert.IsFalse(candle.TryLight());
            candle.Tick(10000f, false);
            candle.Tick(10000f, true);
            Assert.IsTrue(candle.IsLit);
            Assert.IsTrue(candle.IsSafeZone);
            Assert.AreEqual(float.PositiveInfinity, candle.Remaining);
        }

        [TestCase(false, 89.9f)]
        [TestCase(true, 44.9f)]
        public void KindledCandlesExpireAndCanBeRelit(bool raining, float beforeExpiry)
        {
            var candle = new Candle(CandleKind.Kindled, Vector3.zero);
            Assert.IsFalse(candle.IsLit);
            Assert.IsFalse(candle.IsSafeZone);
            Assert.AreEqual(0f, candle.Remaining);
            Assert.IsTrue(candle.TryLight());
            Assert.AreEqual(90f, candle.Remaining);
            candle.Tick(beforeExpiry, raining);
            Assert.IsTrue(candle.IsLit);
            Assert.IsTrue(candle.IsSafeZone);
            float remaining = candle.Remaining;
            Assert.IsFalse(candle.TryLight());
            Assert.AreEqual(remaining, candle.Remaining);
            candle.Tick(0.2f, raining);
            Assert.IsFalse(candle.IsLit);
            Assert.IsFalse(candle.IsSafeZone);
            Assert.AreEqual(0f, candle.Remaining);
            Assert.IsTrue(candle.TryLight());
            Assert.AreEqual(90f, candle.Remaining);
            candle.Tick(89.9f, false);
            Assert.IsTrue(candle.IsLit);
            candle.Tick(0.2f, false);
            Assert.IsFalse(candle.IsLit);
        }

        [Test]
        public void InitiallyLitCandleBurnsFasterWhenRainStarts()
        {
            var candle = new Candle(CandleKind.Kindled, Vector3.zero, true);
            Assert.IsTrue(candle.IsLit);
            Assert.AreEqual(90f, candle.Remaining);
            candle.Tick(30f, false);
            Assert.AreEqual(60f, candle.Remaining);
            candle.Tick(29.9f, true);
            Assert.IsTrue(candle.IsLit);
            candle.Tick(0.2f, true);
            Assert.AreEqual(0f, candle.Remaining);
            Assert.IsFalse(candle.IsLit);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void InvalidTimeDoesNotBurnCandle(float dt)
        {
            var candle = new Candle(CandleKind.Kindled, Vector3.zero, true);
            candle.Tick(dt, false);
            candle.Tick(dt, true);
            Assert.AreEqual(90f, candle.Remaining);
        }

        [Test]
        public void OnlyFamilyCandlesChangeRespawnPoint()
        {
            var point = new RespawnPoint(Vector3.up);
            Assert.AreEqual(Vector3.up, point.Position);
            point.Touch(null);
            Assert.AreEqual(Vector3.up, point.Position);
            point.Touch(new Candle(CandleKind.Kindled, Vector3.zero));
            Assert.AreEqual(Vector3.up, point.Position);
            point.Touch(new Candle(CandleKind.Kindled, Vector3.zero, true));
            Assert.AreEqual(Vector3.up, point.Position);
            point.Touch(new Candle(CandleKind.Family, Vector3.right));
            Assert.AreEqual(Vector3.right, point.Position);
            point.Touch(new Candle(CandleKind.Family, Vector3.forward));
            Assert.AreEqual(Vector3.forward, point.Position);
        }

        [TestCase(100f)]
        [TestCase(10f)]
        public void RespawnRestoresHalfMaximumEnergyAtLastFamilyCandle(float startEnergy)
        {
            var point = new RespawnPoint(Vector3.zero);
            var energy = new SoulEnergy(startEnergy);
            point.Touch(new Candle(CandleKind.Family, Vector3.one));
            Assert.AreEqual(Vector3.one, point.Respawn(energy));
            Assert.AreEqual(50f, energy.Current, 1e-4f);
            Assert.Throws<ArgumentNullException>(() => point.Respawn(null));
        }
    }
}
