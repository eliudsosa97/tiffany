using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class ReturnEndingTests
    {
        MemoryCollector collector;
        PlayerSoul soul;
        PlayerRespawn respawn;
        WitchController witch;
        EndingController ending;
        GameInterface ui;
        Transform route;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = GameInput.FlashOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            // Heartbeat registra cada cambio de ritmo y estos tests no admiten logs inesperados.
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
            collector = Object.FindAnyObjectByType<MemoryCollector>();
            ending = Object.FindAnyObjectByType<EndingController>();
            Assert.IsNotNull(collector);
            Assert.IsNotNull(ending);
            soul = collector.soul;
            respawn = soul.GetComponent<PlayerRespawn>();
            witch = collector.tyler.witch;
            ui = collector.gameInterface;
            route = GameObject.Find("Route").transform;
            Assert.IsNotNull(respawn);
            Assert.AreSame(witch, respawn.witch);
            Assert.AreSame(soul, ending.soul);
            Assert.AreSame(witch, ending.witch);
            Assert.AreSame(ui, ending.gameInterface);
            Assert.AreSame(route.GetChild(7).Find("Window Candle"), ending.transform);
            collector.tyler.Appear(soul.transform.position);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(ReturnEndingTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator FifthMemoryStartsGroundChaseThroughLakeRoadAndHouse()
        {
            for (int id = 1; id <= 4; id++)
            {
                LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo {id} encontrado");
                Assert.IsTrue(collector.Collect(id));
                Assert.AreEqual(Chapter.Cemetery, witch.chapter);
            }
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo 5 encontrado");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tyler: None");
            MovePlayer(FindMemorySpot(5).transform.position - Vector3.forward * 0.5f);
            GameInput.InteractOverride = true;
            yield return null;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsTrue(soul.Powers.Has(Power.Manifest));
            Assert.IsFalse(FindMemorySpot(5).visual.activeSelf);
            Assert.IsFalse(collector.Collect(5));
            Assert.AreEqual(Chapter.Return, witch.chapter);
            Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
            Assert.IsTrue(witch.figure.activeSelf);
            Assert.IsFalse(witch.fireBall.activeSelf);
            Assert.IsFalse(witch.fireLight.enabled);
            Assert.IsTrue(witch.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.IsFalse(SafeZones.Contains(witch.transform.position));
            Assert.Less(Vector3.Distance(soul.transform.position, witch.transform.position), 18f);
            for (int clearing = 5; clearing < 8; clearing++)
            {
                MovePlayer(route.GetChild(clearing).position - Vector3.forward * 12f);
                yield return null;
                yield return null;
                Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
                Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
                Assert.AreEqual(2.5f, witch.Decision.Speed);
                Assert.Less(Vector3.Distance(soul.transform.position, witch.Decision.MoveTarget), 0.1f);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FifthMemoryDuringLandingStartsReturnNearTheHut()
        {
            MovePlayer(route.GetChild(4).position - Vector3.forward * 12f);
            yield return null;
            yield return null;
            Assert.AreEqual(Chapter.WitchHut, witch.chapter);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Landing");
            Object.FindAnyObjectByType<Storm>().raining = true;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (witch.Decision.State != WitchState.Landing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(WitchState.Landing, witch.Decision.State);
            MovePlayer(FindMemorySpot(5).transform.position);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo 5 encontrado");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tyler: None");
            Assert.IsTrue(collector.Collect(5));
            yield return null;
            yield return null;
            Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
            Assert.Less(Vector3.Distance(soul.transform.position, witch.transform.position), 18f);
            Assert.IsTrue(witch.GetComponent<NavMeshAgent>().isOnNavMesh);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CaptureReturnsToLatestCandleWithMemoriesAndGroundChaseIntact()
        {
            collector.tyler.enabled = false;
            var lakeCandle = route.GetChild(5).GetComponentInChildren<CandleSpot>();
            var roadCandle = route.GetChild(6).GetComponentInChildren<CandleSpot>();
            MovePlayer(lakeCandle.transform.position - Vector3.forward);
            yield return null;
            yield return null;
            Assert.AreEqual(lakeCandle.Candle.Position, soul.Respawn.Position);
            MovePlayer(roadCandle.transform.position - Vector3.forward);
            yield return null;
            yield return null;
            Assert.AreEqual(roadCandle.Candle.Position, soul.Respawn.Position);
            MovePlayer(route.GetChild(6).position - Vector3.forward * 4f);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo 3 encontrado");
            Assert.IsTrue(collector.Collect(3));
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Recuerdo 5 encontrado");
            Assert.IsTrue(collector.Collect(5));
            soul.Energy.ResetTo(80f);
            int caught = 0;
            Vector3 caughtPosition = Vector3.zero;
            witch.Caught += () =>
            {
                caughtPosition = soul.transform.position;
                caught++;
            };
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: WindUp");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Sprinting");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Caught");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (caught == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, caught);
            Assert.IsTrue(respawn.Active);
            Assert.IsFalse(soul.GetComponent<PlayerController>().enabled);
            Assert.AreEqual(caughtPosition, soul.transform.position);
            GameInput.MoveOverride = Vector2.right;
            yield return null;
            Assert.AreEqual(caughtPosition, soul.transform.position);
            GameInput.MoveOverride = Vector2.zero;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Atrapada: regreso a la veladora");
            deadline = Time.realtimeSinceStartup + 4f;
            while (respawn.Active && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(respawn.Active);
            Vector3 offset = soul.transform.position - roadCandle.Candle.Position;
            offset.y = 0f;
            Assert.Less(Vector3.Distance(Vector3.back * respawn.sideOffset, offset), 0.01f);
            Assert.That(soul.Energy.Current, Is.InRange(50f,
                50f + SoulCosts.RechargePerSecond * CaughtSequence.FadeInDuration + 1f));
            Assert.IsTrue(soul.GetComponent<PlayerController>().enabled);
            Assert.IsFalse(respawn.overlay.gameObject.activeSelf);
            Assert.IsTrue(SafeZones.Contains(soul.transform.position));
            Assert.IsFalse(SafeZones.Contains(witch.transform.position));
            Assert.IsTrue(soul.Powers.Has(Power.Poltergeist));
            Assert.IsTrue(soul.Powers.Has(Power.Manifest));
            Assert.IsFalse(collector.Collect(5));
            yield return new WaitForSeconds(8.2f);
            Assert.AreEqual(1, caught);
            Assert.AreEqual(Chapter.Return, witch.chapter);
            Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
            Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
            Assert.IsFalse(witch.fireBall.activeSelf);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: WindUp");
            MovePlayer(route.GetChild(6).position - Vector3.forward * 4f);
            yield return null;
            yield return null;
            Assert.AreEqual(2.5f, witch.Decision.Speed);
            deadline = Time.realtimeSinceStartup + 8f;
            while (witch.Decision.State != WitchState.WindUp && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(WitchState.WindUp, witch.Decision.State);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ManifestNeedsPowerProximityInteractionAndUnpausedPlayThenShowsCredits()
        {
            Vector3 besideWindow = ending.transform.position - Vector3.forward * 1.5f;
            besideWindow.y = route.GetChild(7).position.y;
            MovePlayer(besideWindow);
            GameInput.InteractOverride = true;
            yield return null;
            yield return null;
            Assert.AreEqual(EndingStage.Idle, ending.Rules.Stage);
            Assert.IsFalse(ui.endingPanel.gameObject.activeSelf);
            soul.Powers.Unlock(Power.Manifest);
            MovePlayer(besideWindow - Vector3.forward * 4f);
            yield return null;
            yield return null;
            Assert.AreEqual(EndingStage.Idle, ending.Rules.Stage);
            GameInput.InteractOverride = false;
            MovePlayer(besideWindow);
            yield return null;
            Assert.AreEqual(EndingStage.Idle, ending.Rules.Stage);
            ui.TogglePause();
            GameInput.InteractOverride = true;
            yield return null;
            Assert.AreEqual(EndingStage.Idle, ending.Rules.Stage);
            ui.TogglePause();
            yield return null;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.AreEqual(EndingStage.Fade, ending.Rules.Stage);
            Assert.IsFalse(witch.gameObject.activeSelf);
            Assert.IsTrue(ui.endingPanel.gameObject.activeInHierarchy);
            Assert.IsFalse(ui.titlePanel.activeSelf);
            Assert.IsFalse(ui.pausePanel.activeSelf);
            Assert.IsFalse(ui.notebookPanel.activeSelf);
            Assert.IsFalse(ui.noticeText.gameObject.activeSelf);
            Assert.IsFalse(ui.subtitleText.gameObject.activeSelf);
            Assert.AreEqual(InterfaceScreen.Ending, ui.Screen);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsEmpty(ui.endingText.text);
            Assert.That(ui.endingPanel.color.a, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.AreEqual(Vector2.zero, ui.endingPanel.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, ui.endingPanel.rectTransform.anchorMax);
            ui.TogglePause();
            ui.ToggleNotebook();
            ui.StartGame();
            Assert.AreEqual(InterfaceScreen.Ending, ui.Screen);
            Assert.IsTrue(ui.endingPanel.gameObject.activeInHierarchy);
            Assert.IsFalse(ui.titlePanel.activeSelf);
            Assert.IsTrue(ui.IsFrozen);
            Assert.IsTrue(AudioListener.pause);
            float deadline = Time.realtimeSinceStartup + EndingRules.FadeSeconds + 2f;
            while (ending.Rules.Stage == EndingStage.Fade && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(EndingStage.Text, ending.Rules.Stage);
            Assert.AreEqual(InterfaceTexts.EndingStory, ui.endingText.text);
            Assert.AreEqual(Color.white, ui.endingPanel.color);
            deadline = Time.realtimeSinceStartup + EndingRules.TextSeconds + 2f;
            while (ending.Rules.Stage != EndingStage.Credits && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(EndingStage.Credits, ending.Rules.Stage);
            Assert.AreEqual(InterfaceTexts.EndingCredits, ui.endingText.text);
            StringAssert.Contains("Gracias por jugar", ui.endingText.text);
            StringAssert.Contains("Eliud Sosa y Claudia.", ui.endingText.text);
            Assert.IsTrue(ui.IsFrozen);
            Assert.IsFalse(witch.gameObject.activeSelf);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EndingCapturesHoldFadeAndCreditsAndRestoreGameplay()
        {
            var stages = route.GetChild(7).GetComponentsInChildren<EndingCaptureStage>();
            Assert.AreEqual(2, stages.Length);
            System.Array.Sort(stages, (a, b) => string.CompareOrdinal(a.name, b.name));
            Assert.AreEqual("Ending 1 Fade", stages[0].name);
            Assert.AreEqual("Ending 2 Credits", stages[1].name);
            for (int i = 0; i < stages.Length; i++)
            {
                Assert.AreSame(ending, stages[i].ending);
                Assert.AreEqual(i == 1, stages[i].credits);
                Assert.IsNotNull(stages[i].GetComponent<CaptureStop>());
                stages[i].Apply();
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.AreEqual(i == 1 ? 1f : 0.5f, ui.endingPanel.color.a);
                Assert.AreEqual(i == 1 ? InterfaceTexts.EndingCredits : string.Empty, ui.endingText.text);
                Assert.AreEqual(EndingStage.Idle, ending.Rules.Stage);
                Assert.IsFalse(witch.gameObject.activeSelf);
                stages[i].Clear();
                Assert.IsTrue(witch.gameObject.activeSelf);
                Assert.IsTrue(ending.enabled);
                Assert.IsFalse(ui.endingPanel.gameObject.activeSelf);
                Assert.IsFalse(ui.IsFrozen);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsFalse(AudioListener.pause);
            }
            LogAssert.NoUnexpectedReceived();
        }

        MemorySpot FindMemorySpot(int memoryId)
        {
            foreach (var spot in collector.spots)
                if (spot != null && spot.memoryId == memoryId) return spot;
            Assert.Fail($"No se encontró el recuerdo {memoryId}.");
            return null;
        }

        void MovePlayer(Vector3 position)
        {
            var character = soul.GetComponent<CharacterController>();
            character.enabled = false;
            soul.transform.SetPositionAndRotation(position, Quaternion.identity);
            character.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
