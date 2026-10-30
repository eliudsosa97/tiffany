using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class WitchChapterTrackerTests : TiffanyLogTests
    {
        private WitchChapterTracker tracker;
        private WitchController witch;
        private MemoryCollector collector;
        private Transform route;
        private CharacterController character;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = GameInput.LightHeldOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
            route = GameObject.Find("Route").transform;
            tracker = route.GetComponent<WitchChapterTracker>();
            collector = Object.FindAnyObjectByType<MemoryCollector>();
            Assert.IsNotNull(tracker);
            Assert.IsNotNull(collector);
            witch = collector.tyler.witch;
            character = collector.GetComponent<CharacterController>();
            Assert.AreSame(witch, tracker.witch);
            Assert.AreSame(collector.transform, tracker.player);
            Assert.AreEqual(8, tracker.clearings.Length);
            for (int i = 0; i < 8; i++) Assert.AreSame(route.GetChild(i), tracker.clearings[i]);
            Assert.AreEqual(Chapter.Cemetery, witch.chapter);
            Assert.AreEqual(witch.skyRoute[0].y + WitchProfiles.For(Chapter.Cemetery).FlightHeightOffset,
                witch.transform.position.y, 0.01f);
            witch.enabled = false;
            collector.tyler.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(WitchChapterTrackerTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator WaterPlantThenStableThenCemeteryKeepsStable()
        {
            Vector3 position = witch.transform.position;
            WitchDecision decision = witch.Decision;
            yield return MoveToClearing(2);
            Assert.AreEqual(Chapter.WaterPlant, witch.chapter);
            yield return MoveToClearing(4);
            Assert.AreEqual(Chapter.Stable, witch.chapter);
            yield return MoveToClearing(1);
            Assert.AreEqual(Chapter.Stable, witch.chapter);
            Assert.AreEqual(position, witch.transform.position);
            Assert.AreEqual(decision, witch.Decision);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EveryClearingAdvancesOnlyAsFarAsWitchHut()
        {
            for (int clearing = 1; clearing <= 8; clearing++)
            {
                yield return MoveToClearing(clearing);
                var expected = clearing <= 5 ? (Chapter)(clearing - 1) : Chapter.WitchHut;
                Assert.AreEqual(expected, witch.chapter);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PathsAndTestZoneLeaveTheChapterUnchanged()
        {
            yield return MovePlayer(route.GetChild(0).Find("Path to Next Clearing").position);
            Assert.AreEqual(Chapter.Cemetery, witch.chapter);
            Vector3 testZone = GameObject.Find("Test Zone").transform.Find("Spawn").position;
            yield return MovePlayer(testZone);
            Assert.AreEqual(Chapter.Cemetery, witch.chapter);
            yield return MoveToClearing(4);
            yield return MovePlayer(route.GetChild(1).Find("Path to Next Clearing").position);
            Assert.AreEqual(Chapter.Stable, witch.chapter);
            yield return MovePlayer(testZone);
            Assert.AreEqual(Chapter.Stable, witch.chapter);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FifthMemoryLocksReturnAcrossAllClearings()
        {
            foreach (var challenge in collector.challenges)
            {
                challenge.Rules.Tick(-1f, true, 3, true, true);
                challenge.Rules.Tick(ChallengeRules.PassageLength, true, 3, true, true);
            }
            yield return MoveToClearing(5);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo 5 encontrado");
            Assert.IsTrue(collector.Collect(5));
            for (int clearing = 8; clearing >= 1; clearing--)
            {
                yield return MoveToClearing(clearing);
                Assert.AreEqual(Chapter.Return, witch.chapter);
                Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
            }
            witch.SetChapter(Chapter.WitchHut);
            Assert.AreEqual(Chapter.Return, witch.chapter);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator MoveToClearing(int clearing) =>
            MovePlayer(route.GetChild(clearing - 1).position - Vector3.forward * 12f);

        [UnityTest]
        public IEnumerator ControllerReceivesNextClearingAndTouchedFamilyCandleThroughSenses()
        {
            witch.enabled = true;
            yield return MoveToClearing(2);
            Assert.AreEqual(1, tracker.LastVisitedClearingIndex);
            Assert.IsTrue(witch.Perception.HasNextClearing);
            Assert.AreEqual(tracker.ClearingBounds[2].center, witch.Perception.NextClearingPosition);
            yield return MoveToClearing(1);
            Assert.AreEqual(tracker.ClearingBounds[1].center, witch.Perception.NextClearingPosition,
                "El siguiente claro sigue a la última visita, incluso al retroceder, sin bajar el capítulo.");
            CandleSpot family = null;
            foreach (var candle in Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None))
                if (candle.kind == CandleKind.Family) { family = candle; break; }
            Assert.IsNotNull(family, "La escena generada debe incluir veladoras de la familia.");
            yield return MovePlayer(family.transform.position);
            yield return null;
            var respawn = collector.GetComponent<PlayerSoul>().Respawn;
            Assert.IsTrue(respawn.HasFamilyCandle);
            Assert.IsTrue(witch.Perception.HasFamilyCandle && witch.Perception.PlayerAtFamilyCandle);
            Assert.AreEqual(respawn.Position, witch.Perception.FamilyCandlePosition);
            Assert.AreEqual(family.radius, witch.Perception.FamilyCandleRadius);
            yield return MoveToClearing(8);
            Assert.IsFalse(tracker.HasNextClearing);
            Assert.IsFalse(witch.Perception.HasNextClearing);
        }

        [UnityTest]
        public IEnumerator BeforeAnyClearingTheNextOneIsTheFirst()
        {
            var probeObject = new GameObject("Witch Tracker Before Route");
            probeObject.transform.SetParent(route, false);
            var probe = probeObject.AddComponent<WitchChapterTracker>();
            probe.clearings = tracker.clearings;
            probe.player = GameObject.Find("Test Zone").transform.Find("Spawn");
            yield return null;
            Assert.AreEqual(-1, probe.LastVisitedClearingIndex);
            Assert.IsTrue(probe.HasNextClearing);
            Assert.AreEqual(tracker.ClearingBounds[0].center, probe.NextClearingPosition);
            Object.Destroy(probeObject);
        }

        private IEnumerator MovePlayer(Vector3 position)
        {
            character.enabled = false;
            collector.transform.SetPositionAndRotation(position, Quaternion.identity);
            character.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }
    }
}
