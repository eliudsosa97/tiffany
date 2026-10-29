using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class CandleKindlerTests : TiffanyLogTests
    {
        private GameObject world;
        private PlayerSoul soul;
        private CandleKindler kindler;
        private CandleSpot candle;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            world = new GameObject("KindleTestWorld");
            world.SetActive(false);
            var player = new GameObject("Tiffany");
            player.transform.SetParent(world.transform, false);
            soul = player.AddComponent<PlayerSoul>();
            soul.startingPowers = new[] { Power.Candles };
            kindler = player.AddComponent<CandleKindler>();
            candle = AddCandle(Vector3.right * 1.5f);
            kindler.candles = new[] { candle };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(world);
            yield return null;
            SafeZones.Clear();
        }

        [UnityTest]
        public IEnumerator LightsNearbyCandleOnceAndSpendsTwentyEnergy()
        {
            world.SetActive(true);
            Assert.IsFalse(candle.Candle.IsLit);
            Assert.IsFalse(candle.flameMesh.enabled);
            Assert.IsFalse(candle.flame.enabled);
            Assert.IsFalse(SafeZones.Contains(candle.transform.position));

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tiffany enciende una veladora");
            Assert.IsTrue(kindler.TryKindle());
            Assert.AreEqual(80f, soul.Energy.Current);
            Assert.IsTrue(candle.Candle.IsLit);

            yield return null;

            Assert.IsTrue(SafeZones.Contains(candle.transform.position));
            Assert.IsTrue(candle.flameMesh.enabled);
            Assert.IsTrue(candle.flame.enabled);
            Assert.IsFalse(kindler.TryKindle());
            Assert.AreEqual(80f, soul.Energy.Current);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RainRemovesKindledSafetyAndKeepsFamilyLit()
        {
            candle.startLit = true;
            var family = AddCandle(Vector3.right * 10f, true);
            family.kind = CandleKind.Family;
            var stormObject = new GameObject("Storm");
            stormObject.transform.SetParent(world.transform, false);
            var storm = stormObject.AddComponent<Storm>();
            candle.storm = family.storm = storm;
            world.SetActive(true);
            Assert.IsTrue(SafeZones.Contains(candle.transform.position));

            storm.raining = true;
            yield return null;

            Assert.IsFalse(candle.Candle.IsLit);
            Assert.IsFalse(candle.flame.enabled);
            Assert.IsFalse(candle.flameMesh.enabled);
            Assert.IsFalse(SafeZones.Contains(candle.transform.position));
            Assert.IsTrue(family.Candle.IsLit);
            Assert.IsTrue(family.flame.enabled);
            Assert.IsTrue(SafeZones.Contains(family.transform.position));
            Assert.IsFalse(kindler.TryKindle());
            Assert.AreEqual(100f, soul.Energy.Current);
        }

        [Test]
        public void RainBlocksKindlingBeforeTheCandleTicks()
        {
            var stormObject = new GameObject("Storm");
            stormObject.transform.SetParent(world.transform, false);
            candle.storm = stormObject.AddComponent<Storm>();
            candle.storm.raining = true;
            world.SetActive(true);

            Assert.IsFalse(kindler.TryKindle());
            Assert.IsFalse(candle.Candle.IsLit);
            Assert.AreEqual(100f, soul.Energy.Current);
        }

        [Test]
        public void IgnoresCandlesBeyondTwoMeters()
        {
            candle.transform.position = Vector3.right * 2.5f;
            world.SetActive(true);

            Assert.IsFalse(kindler.TryKindle());
            Assert.AreEqual(100f, soul.Energy.Current);
            Assert.IsFalse(candle.Candle.IsLit);
        }

        [Test]
        public void CannotKindleWithoutThePower()
        {
            soul.startingPowers = new Power[0];
            world.SetActive(true);

            Assert.IsFalse(kindler.TryKindle());
            Assert.AreEqual(100f, soul.Energy.Current);
            Assert.IsFalse(candle.Candle.IsLit);
        }

        [UnityTest]
        public IEnumerator CaptureCleanupRestoresOnlyTheCandleItLit()
        {
            CandleSpot alreadyLit = AddCandle(Vector3.right * 5f, true);
            kindler.candles = new[] { candle, alreadyLit };
            world.SetActive(true);
            var stop = new GameObject("Candle Capture");
            stop.transform.SetParent(world.transform, false);
            var stage = stop.AddComponent<PowerCaptureStage>();
            stage.action = PowerCaptureAction.Kindle;

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tiffany enciende una veladora");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Captura de poder: veladora encendida");
            stage.Apply(soul.transform);
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(candle.Candle.IsLit);
            Assert.IsTrue(SafeZones.Contains(candle.transform.position));
            Assert.IsTrue(candle.flameMesh.enabled);
            stage.Clear();
            Assert.IsFalse(candle.Candle.IsLit);
            yield return null;

            Assert.IsFalse(SafeZones.Contains(candle.transform.position));
            Assert.IsFalse(candle.flameMesh.enabled);
            Assert.IsFalse(candle.flame.enabled);
            Assert.IsTrue(alreadyLit.Candle.IsLit);
            Assert.IsTrue(alreadyLit.flameMesh.enabled);
            Assert.IsTrue(SafeZones.Contains(alreadyLit.transform.position));
            LogAssert.NoUnexpectedReceived();
        }

        private CandleSpot AddCandle(Vector3 position, bool startLit = false)
        {
            var instance = new GameObject("Kindled Candle");
            instance.transform.SetParent(world.transform, false);
            instance.transform.position = position;
            var spot = instance.AddComponent<CandleSpot>();
            spot.kind = CandleKind.Kindled;
            spot.startLit = startLit;
            spot.player = soul;
            spot.flameMesh = instance.AddComponent<MeshRenderer>();
            spot.flame = instance.AddComponent<Light>();
            return spot;
        }
    }
}
