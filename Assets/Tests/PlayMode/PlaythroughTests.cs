using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughTests
    {
        Playthrough runner;
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
            Application.logMessageReceived += RecordMilestone;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= RecordMilestone;
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
            var plan = new PlaythroughPlan(new[]
            {
                PlaythroughMilestone.Memory1, PlaythroughMilestone.Ball, PlaythroughMilestone.Memory2
            }, 55f);
            ExpectMilestone("lápida \\(recuerdo 1\\)");
            ExpectMilestone("pelota y Tyler");
            ExpectMilestone("listón \\(recuerdo 2\\)");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Tiffany\] Partida completa en \d+\.\ds$"));
            runner = new GameObject("Playthrough Test").AddComponent<Playthrough>();
            runner.Configure(plan);
            float start = Time.time;
            float realDeadline = Time.realtimeSinceStartup + 90f;
            while (!plan.Finished && Time.time - start < 60f && Time.realtimeSinceStartup < realDeadline) yield return null;

            Assert.IsTrue(plan.Finished, "No terminó el recorrido reducido.");
            Assert.AreEqual(0, plan.ExitCode, plan.FailureReason);
            Assert.AreEqual(3, plan.CompletedCount);
            Assert.Less(Time.time - start, 60f);
            Assert.AreEqual(3, milestones.Count);
            StringAssert.EndsWith("lápida (recuerdo 1)", milestones[0]);
            StringAssert.EndsWith("pelota y Tyler", milestones[1]);
            StringAssert.EndsWith("listón (recuerdo 2)", milestones[2]);
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            Assert.IsTrue(collector.IsFound(1));
            Assert.IsTrue(collector.IsFound(2));
            Assert.IsFalse(collector.IsFound(3));
            Assert.Less(PlaythroughPlan.HorizontalDistance(collector.transform.position, collector.spots[1].transform.position), 2f);
            Assert.IsNull(GameInput.MoveOverride);
            Assert.IsNull(GameInput.InteractOverride);
        }

        [UnityTest]
        public IEnumerator FullRouteReusesTemporaryPropsAndHasPathsToEveryClearing()
        {
            var collector = Object.FindAnyObjectByType<MemoryCollector>();
            var kindler = collector.GetComponent<CandleKindler>();
            var originalCandle = kindler.candles[0];
            Vector3 originalPosition = originalCandle.transform.position;
            int originalObjects = MovableObject.Active.Count;
            var route = GameObject.Find("Route").transform;
            using (var scene = new PlaythroughScene())
            {
                scene.Prepare(new PlaythroughPlan(), _ => { });
                yield return null;
                Assert.AreNotSame(originalCandle, kindler.candles[0]);
                Assert.IsTrue(kindler.candles[0].transform.IsChildOf(route.GetChild(1)));
                Assert.AreEqual(kindler.candles[0].transform.position, kindler.candles[0].Candle.Position);
                Assert.AreEqual(originalPosition, originalCandle.transform.position);
                Assert.AreEqual(originalObjects + 1, MovableObject.Active.Count);
                scene.Select(PlaythroughMilestone.Hide);
                Assert.Less(PlaythroughPlan.HorizontalDistance(scene.Target, route.GetChild(0).position), 18f);
                var navigation = new PlaythroughNavigation();
                var targets = new[]
                {
                    PlaythroughMilestone.Hide, PlaythroughMilestone.Memory2, PlaythroughMilestone.Kindle,
                    PlaythroughMilestone.Memory3, PlaythroughMilestone.MoveObject, PlaythroughMilestone.Memory4,
                    PlaythroughMilestone.Memory5, PlaythroughMilestone.LakeCandle, PlaythroughMilestone.Road,
                    PlaythroughMilestone.Manifest
                };
                foreach (var target in targets)
                {
                    scene.Select(target);
                    Assert.IsTrue(navigation.Calculate(scene.Position, scene.Target), target.ToString());
                }
            }
            yield return null;
            Assert.AreSame(originalCandle, kindler.candles[0]);
            Assert.AreEqual(originalObjects, MovableObject.Active.Count);
            Assert.IsTrue(collector.tyler.gameObject.activeSelf);
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
            if (type == LogType.Log && Regex.IsMatch(message, @"^\[Tiffany\] Partida \d+\.\ds ")) milestones.Add(message);
        }
    }
}
