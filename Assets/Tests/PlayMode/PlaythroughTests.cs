using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughTests
    {
        Playthrough runner;
        Vector3 ballVisitPosition;
        readonly List<string> milestones = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.ClearOverrides();
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
            milestones.Clear();
            ballVisitPosition = Vector3.positiveInfinity;
            Application.logMessageReceived += RecordMilestone;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= RecordMilestone;
            Playthrough.CaptureStartupErrors(System.Array.Empty<string>());
            if (runner != null) Object.Destroy(runner.gameObject);
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(PlaythroughTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator WalksFromGravestoneAndBallToSecondMemoryInLessThanSixtySeconds()
        {
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            Assert.IsFalse(collector.tyler.gameObject.activeSelf);
            var ball = GameObject.Find("Route").transform.GetChild(0).Find("Ball");
            var secondMemory = System.Array.Find(collector.spots, spot => spot != null && spot.memoryId == 2);
            Assert.IsNotNull(secondMemory);
            var plan = new PlaythroughPlan(new[]
            {
                PlaythroughMilestone.Memory1, PlaythroughMilestone.Ball, PlaythroughMilestone.Memory2
            }, 55f);
            ExpectMilestone("lápida \\(recuerdo 1\\)");
            ExpectMilestone("recoger la pelota");
            ExpectMilestone("listón \\(recuerdo 2\\)");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa en \d+\.\ds$"));
            runner = new GameObject("Playthrough Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float start = Time.time;
            float realDeadline = Time.realtimeSinceStartup + 90f;
            while (!plan.Finished && Time.time - start < 60f && Time.realtimeSinceStartup < realDeadline) yield return null;

            Assert.IsTrue(plan.Finished, "No terminó el recorrido reducido.");
            Assert.AreEqual(0, plan.ExitCode, plan.FailureReason);
            Assert.IsEmpty(plan.MissingContent);
            Assert.IsNull(plan.FailureReason);
            Assert.IsNull(plan.FirstConsoleError);
            Assert.AreEqual(3, plan.CompletedCount);
            Assert.Less(Time.time - start, 60f);
            Assert.AreEqual(3, milestones.Count);
            StringAssert.EndsWith("lápida (recuerdo 1)", milestones[0]);
            StringAssert.EndsWith("recoger la pelota", milestones[1]);
            StringAssert.EndsWith("listón (recuerdo 2)", milestones[2]);
            Assert.LessOrEqual(PlaythroughPlan.HorizontalDistance(ballVisitPosition, ball.position), 1f);
            Assert.IsTrue(collector.IsFound(MemoryProgress.BallId));
            Assert.IsFalse(ball.gameObject.activeSelf);
            Assert.IsTrue(collector.tyler.gameObject.activeSelf);
            Assert.IsTrue(collector.IsFound(1));
            Assert.IsTrue(collector.IsFound(2));
            Assert.IsFalse(collector.IsFound(3));
            Assert.Less(PlaythroughPlan.HorizontalDistance(collector.transform.position, secondMemory.transform.position), 2f);
            Assert.IsNull(GameInput.MoveOverride);
            Assert.IsNull(GameInput.InteractOverride);
        }

        [UnityTest]
        public IEnumerator MissingBallIsReportedWithoutFailingSceneSetup()
        {
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            Object.Destroy(GameObject.Find("Route").transform.GetChild(0).Find("Ball").gameObject);
            yield return null;
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Ball }, 5f);
            ExpectMilestone("faltante: recogida real de la pelota");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa con faltantes en \d+\.\ds: recogida real de la pelota$"));
            runner = new GameObject("Playthrough Missing Ball Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(2, plan.ExitCode, plan.FailureReason);
            CollectionAssert.AreEqual(new[] { "recogida real de la pelota" }, plan.MissingContent);
            Assert.IsFalse(collector.IsFound(MemoryProgress.BallId));
            Assert.IsFalse(collector.tyler.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator FullPlaythroughSolvesAllFourChallengesBeforeReturningHome()
        {
            var plan = new PlaythroughPlan();
            runner = new GameObject("Full Challenge Playthrough Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            var respawn = Object.FindAnyObjectByType<PlayerRespawn>();
            float deadline = Time.realtimeSinceStartup + 600f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline)
            {
                if (PlaythroughPlan.ChallengeIndex(plan.Current) >= 0)
                    Assert.IsFalse(respawn.Active, "La partida automática debe resolver los retos sin capturas.");
                yield return null;
            }
            Assert.IsTrue(plan.Finished, PlaythroughPlan.Label(plan.Current));
            Assert.AreEqual(0, plan.ExitCode, plan.FailureReason);
            Assert.IsEmpty(plan.MissingContent);
            var challenges = Object.FindObjectsByType<PowerChallenge>(FindObjectsSortMode.None);
            Assert.AreEqual(4, challenges.Length);
            foreach (var challenge in challenges) Assert.IsTrue(challenge.Rules.Completed, challenge.name);
            Assert.AreEqual(EndingStage.Credits, Object.FindAnyObjectByType<EndingController>().Rules.Stage);
        }

        [UnityTest]
        public IEnumerator FullRoutePreservesOriginalPropsAndHasPathsToEveryClearing()
        {
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            var kindler = collector.GetComponent<CandleKindler>();
            var originalCandle = kindler.candles[0];
            Vector3 originalPosition = originalCandle.transform.position;
            var originalCandles = (CandleSpot[])kindler.candles.Clone();
            var originalObjects = new List<MovableObject>(MovableObject.Active);
            var originalHides = Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None);
            var allCandles = Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None);
            bool tylerWasActive = collector.tyler.gameObject.activeSelf;
            var route = GameObject.Find("Route").transform;
            var scene = new PlaythroughScene();
            scene.Prepare(new PlaythroughPlan(new[] { PlaythroughMilestone.Kindle, PlaythroughMilestone.MoveObject }), _ => { });
            yield return null;
            CollectionAssert.AreEqual(originalCandles, kindler.candles);
            CollectionAssert.AreEquivalent(allCandles, Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None));
            CollectionAssert.AreEqual(originalObjects, MovableObject.Active);
            CollectionAssert.AreEquivalent(originalHides, Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None));
            Assert.AreEqual(originalPosition, originalCandle.transform.position);
            Assert.AreEqual(tylerWasActive, collector.tyler.gameObject.activeSelf);
            scene.Select(PlaythroughMilestone.Kindle);
            Assert.AreEqual(originalPosition + Vector3.back * 1.4f, scene.Target);
            Assert.IsTrue(originalCandle.transform.IsChildOf(route.GetChild(0)));
            scene.Select(PlaythroughMilestone.MoveObject);
            var originalObject = originalObjects.Find(item => item.transform.IsChildOf(route.GetChild(0)));
            Assert.AreEqual(originalObject.transform.position + Vector3.back * 2f, scene.Target);
            var navigation = new PlaythroughNavigation();
            var targets = new[]
            {
                PlaythroughMilestone.Hide, PlaythroughMilestone.Memory2, PlaythroughMilestone.Kindle, PlaythroughMilestone.Memory3,
                PlaythroughMilestone.MoveObject, PlaythroughMilestone.Memory4, PlaythroughMilestone.Memory5,
                PlaythroughMilestone.LakeCandle, PlaythroughMilestone.Road, PlaythroughMilestone.Manifest
            };
            foreach (var target in targets)
            {
                scene.Select(target);
                Assert.IsTrue(navigation.Calculate(scene.Position, scene.Target), target.ToString());
            }
        }

        [UnityTest]
        public IEnumerator UsesOriginalCandleAndMovableObjectThroughInput()
        {
            var kindler = Object.FindAnyObjectByType<CandleKindler>();
            kindler.GetComponent<PlayerSoul>().Powers.Unlock(Power.Candles);
            kindler.GetComponent<PlayerSoul>().Powers.Unlock(Power.Poltergeist);
            var originalCandle = kindler.candles[0];
            var originalObjects = new List<MovableObject>(MovableObject.Active);
            var plan = new PlaythroughPlan(new[]
            {
                PlaythroughMilestone.Light, PlaythroughMilestone.Flash,
                PlaythroughMilestone.Kindle, PlaythroughMilestone.MoveObject
            }, 20f);
            ExpectMilestone("luz");
            ExpectMilestone("destello");
            ExpectMilestone("encender una veladora");
            ExpectMilestone("mover un objeto");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa en \d+\.\ds$"));
            runner = new GameObject("Playthrough Powers Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float deadline = Time.realtimeSinceStartup + 40f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(0, plan.ExitCode, plan.FailureReason);
            Assert.AreSame(originalCandle, kindler.candles[0]);
            Assert.IsTrue(originalCandle.Candle.IsLit);
            Assert.IsTrue(originalObjects.Exists(item => item.Used && item.GetComponentInParent<PowerChallenge>() == null));
            CollectionAssert.AreEqual(originalObjects, MovableObject.Active);
        }

        [UnityTest]
        public IEnumerator DisconnectedHideIsReportedAndBothHideStepsAreSkipped()
        {
            foreach (var hide in Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None))
                hide.transform.position += Vector3.right * 1000f;
            Physics.SyncTransforms();
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide });
            ExpectMilestone("faltante: escondite alcanzable por NavMesh desde la ruta");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa con faltantes en \d+\.\ds: escondite alcanzable por NavMesh desde la ruta$"));
            runner = new GameObject("Playthrough Missing Hide Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(2, plan.ExitCode, plan.FailureReason);
            Assert.AreEqual(2, plan.CompletedCount);
            Assert.AreEqual(1, milestones.Count);
            Assert.IsFalse(Object.FindAnyObjectByType<PlayerHiding>().Hidden);
        }

        [UnityTest]
        public IEnumerator ReachableRouteHideIsPreferredToCloserTestHideThroughInput()
        {
            var route = GameObject.Find("Route").transform;
            var hide = route.GetChild(0).GetComponentInChildren<HideVolume>();
            var testHide = GameObject.Find("Test Zone/Hide Test").GetComponent<HideVolume>();
            testHide.transform.position = Object.FindAnyObjectByType<PlayerHiding>().transform.position + Vector3.right * 2f;
            Physics.SyncTransforms();
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide }, 15f);
            var scene = new PlaythroughScene();
            scene.Prepare(plan, _ => { });
            scene.Select(PlaythroughMilestone.Hide);
            Assert.IsTrue(NavMesh.SamplePosition(testHide.HidePoint, out _, 0.5f, NavMesh.AllAreas));
            Assert.IsTrue(new PlaythroughNavigation().Calculate(scene.Position, testHide.HidePoint));
            Assert.Less(Vector3.Distance(testHide.HidePoint, scene.Position), Vector3.Distance(hide.HidePoint, scene.Position));
            Assert.AreEqual(hide.HidePoint, scene.Target);
            Assert.IsNull(scene.Observe().MissingContent);
            ExpectMilestone("entrar al escondite");
            ExpectMilestone("salir del escondite");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa en \d+\.\ds$"));
            runner = new GameObject("Playthrough Hide Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(0, plan.ExitCode, plan.FailureReason);
            Assert.IsEmpty(plan.MissingContent);
            Assert.AreEqual(6, Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(Object.FindAnyObjectByType<PlayerHiding>().Hidden);
        }

        [UnityTest]
        public IEnumerator StartupErrorSurvivesSceneLoadingAndOverridesMissingContent()
        {
            Playthrough.CaptureStartupErrors(new[] { "Tiffany", "-playthrough" });
            LogAssert.Expect(LogType.Error, "primer error antes de la escena");
            Debug.LogError("primer error antes de la escena");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: segunda excepción antes de la escena"));
            Debug.LogException(new System.InvalidOperationException("segunda excepción antes de la escena"));
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            var ballPickup = System.Array.Find(collector.spots, spot => spot != null && spot.memoryId == MemoryProgress.BallId);
            Object.Destroy(ballPickup.gameObject);
            yield return null;
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Ball }, 20f);
            ExpectMilestone("faltante: recogida real de la pelota");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida fallida en recoger la pelota: .*primer error antes de la escena$"));
            runner = new GameObject("Playthrough Startup Error Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            Assert.AreEqual("primer error antes de la escena", plan.FirstConsoleError);
            Assert.AreEqual(1, plan.MissingContent.Count);
        }

        [UnityTest]
        public IEnumerator ConsoleErrorIsRememberedUntilTheFinalResult()
        {
            runner = new GameObject("Playthrough Error Test").AddComponent<Playthrough>();
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Memory1 }, 20f);
            runner.Configure(plan);
            LogAssert.Expect(LogType.Error, "error de prueba de partida");
            Debug.LogError("error de prueba de partida");
            ExpectMilestone("lápida \\(recuerdo 1\\)");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida fallida en lápida.*error de prueba de partida$"));
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!plan.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            Assert.AreEqual(1, plan.CompletedCount);
            StringAssert.Contains("error de prueba de partida", plan.FailureReason);
        }

        static void ExpectMilestone(string label) => LogAssert.Expect(LogType.Log,
            new Regex(@"^\[Tiffany\] Partida \d+\.\ds " + label + "$"));

        void RecordMilestone(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log && Regex.IsMatch(message, @"^\[Tiffany\] Partida \d+\.\ds (?!claro: )")) milestones.Add(message);
            if (message.EndsWith("recoger la pelota"))
                ballVisitPosition = Object.FindAnyObjectByType<PlayerController>().transform.position;
        }
    }
}
