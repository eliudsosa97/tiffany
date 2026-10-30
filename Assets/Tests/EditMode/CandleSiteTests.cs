using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class CandleSiteTests
    {
        [TestCase(0f, 0f, 30f)]
        [TestCase(0f, 5f, 30f)]
        [TestCase(3f, 0f, 10f)]
        [TestCase(4f, 0f, 10f)]
        public void FamilyRechargesAndMarksRespawnOnlyInsideHorizontalRadius(float x, float y, float expected)
        {
            var candle = new Candle(CandleKind.Family, new Vector3(-5f, 0f, 4f));
            var site = new CandleSite(candle, CandleSite.DefaultRadius);
            var energy = new SoulEnergy(10f);
            var start = new Vector3(50f, 0f, 50f);
            var respawn = new RespawnPoint(start);

            site.Tick(1f, true, candle.Position + new Vector3(x, y, 0f), energy, respawn);

            Assert.AreEqual(expected, energy.Current);
            Assert.AreEqual(expected > 10f, site.PlayerInside);
            Assert.AreEqual(expected > 10f, site.Recharging);
            Assert.AreEqual(expected > 10f ? candle.Position : start, respawn.Position);
        }

        [Test]
        public void RespawnChangesOnlyOnEntryAndNullVisitorsAreAllowed()
        {
            var site = new CandleSite(new Candle(CandleKind.Family, Vector3.zero), 3f);
            var respawn = new RespawnPoint(Vector3.one);
            site.Tick(1f, false, Vector3.zero, null, respawn);
            respawn.Touch(new Candle(CandleKind.Family, Vector3.one));
            site.Tick(1f, false, Vector3.zero, null, respawn);
            Assert.AreEqual(Vector3.one, respawn.Position);
            site.Tick(1f, false, Vector3.right * 4f, null, null);
            site.Tick(1f, false, Vector3.zero, null, respawn);
            Assert.AreEqual(Vector3.zero, respawn.Position);
            site.Tick(1f, false, Vector3.zero, new SoulEnergy(10f), null);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(2.5f)]
        public void RespawnArrivalFollowsTheLatestFamilyCandleAndSideOffset(float sideOffset)
        {
            var respawn = new RespawnPoint(Vector3.zero);
            var ramp = new CandleSite(new Candle(CandleKind.Family, new Vector3(230f, 2f, 72f)), 3f);
            var lake = new CandleSite(new Candle(CandleKind.Family, new Vector3(190f, 0f, 76f)), 3f);
            ramp.Tick(0f, true, ramp.Candle.Position, null, respawn);
            Vector3 rampArrival = respawn.ArrivalPosition(sideOffset);
            lake.Tick(0f, true, lake.Candle.Position, null, respawn);
            Vector3 arrival = lake.Candle.Position + Vector3.back * sideOffset;
            Assert.AreEqual(arrival, respawn.ArrivalPosition(sideOffset));
            Assert.IsTrue(respawn.IsAtArrival(arrival + Vector3.up * 0.2f, sideOffset, 0.35f));
            Assert.IsFalse(respawn.IsAtArrival(rampArrival, sideOffset, 0.35f), "El checkpoint de la rampa ya fue reemplazado.");
            Assert.IsFalse(respawn.IsAtArrival(arrival + Vector3.right * 0.36f, sideOffset, 0.35f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KindledOnlyProvidesSafetyWhileLit(bool lit)
        {
            var site = new CandleSite(new Candle(CandleKind.Kindled, Vector3.zero, lit), 3f);
            var energy = new SoulEnergy(10f);
            var respawn = new RespawnPoint(Vector3.one);
            site.Tick(1f, false, Vector3.zero, energy, respawn);
            Assert.AreEqual(lit, site.Contains(Vector3.up * 5f));
            Assert.AreEqual(lit, site.PlayerInside);
            Assert.IsFalse(site.Recharging);
            Assert.AreEqual(10f, energy.Current);
            Assert.AreEqual(Vector3.one, respawn.Position);
            site.Tick(0f, true, Vector3.zero, energy, respawn);
            Assert.IsFalse(site.PlayerInside);
            Assert.IsFalse(site.Contains(Vector3.zero));
            Assert.IsFalse(site.Candle.TryLight(true));
            Assert.IsTrue(site.Candle.TryLight(false));
            Assert.IsTrue(site.Contains(Vector3.zero));
        }

        [Test]
        public void RejectsNullCandle() => Assert.Throws<ArgumentNullException>(() => new CandleSite(null, 3f));

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidRadius(float radius) => Assert.Throws<ArgumentOutOfRangeException>(
            () => new CandleSite(new Candle(CandleKind.Family, Vector3.zero), radius));

        [Test]
        public void PlayerSoulRespawnStartsAtObjectPositionAndIsReused()
        {
            var player = new GameObject("Player");
            try
            {
                var soul = player.AddComponent<PlayerSoul>();
                player.transform.position = new Vector3(3f, 0f, 4f);
                var respawn = soul.Respawn;
                Assert.AreEqual(player.transform.position, respawn.Position);
                player.transform.position = Vector3.zero;
                Assert.AreSame(respawn, soul.Respawn);
                Assert.AreEqual(new Vector3(3f, 0f, 4f), soul.Respawn.Position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }
    }
}
