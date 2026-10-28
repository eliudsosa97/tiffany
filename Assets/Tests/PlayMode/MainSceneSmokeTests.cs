using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public class MainSceneSmokeTests
    {
        Scene mainScene;
        List<StepNoise> steps;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            steps = new List<StepNoise>();
            GameInput.MoveOverride = Vector2.zero;
            GameInput.SlowOverride = false;
            GameInput.RunOverride = false;
            GameInput.AimOverride = Vector2.zero;

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            mainScene = SceneManager.GetActiveScene();
            NoiseBus.Published += RecordStep;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordStep;
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(MainSceneSmokeTests));
            yield return SceneManager.UnloadSceneAsync(mainScene);
        }

        [UnityTest]
        public IEnumerator MainSceneHasPlayerCameraAndSixGrounds()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<PlayerController>());
            Assert.IsNotNull(player.GetComponent<CharacterController>());
            var witch = Object.FindAnyObjectByType<WitchController>();
            Assert.IsNotNull(witch);
            var light = player.GetComponent<PlayerLight>();
            Assert.IsNotNull(light);
            Assert.IsFalse(light.startOn);
            Assert.AreSame(witch, light.witch);

            var viewCamera = Camera.main;
            Assert.IsNotNull(viewCamera);
            var follow = viewCamera.GetComponent<FollowCamera>();
            Assert.IsNotNull(follow);
            Assert.AreSame(player.transform, follow.target);

            var surfaces = Object.FindObjectsByType<GroundSurface>(FindObjectsSortMode.None);
            foreach (GroundType type in System.Enum.GetValues(typeof(GroundType)))
            {
                int count = 0;
                foreach (var surface in surfaces)
                    if (surface.type == type) count++;
                Assert.GreaterOrEqual(count, 1, $"Debe haber al menos un suelo de tipo {type}.");
            }

            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainSceneHasMovableObjectsUnderTiffanysPower()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var poltergeist = player.GetComponent<Poltergeist>();
            var playerLight = player.GetComponent<PlayerLight>();
            var soul = player.GetComponent<PlayerSoul>();
            Assert.IsNotNull(poltergeist);
            Assert.IsNotNull(playerLight);
            Assert.IsNotNull(soul);
            Assert.IsNotNull(playerLight.spot);
            Assert.AreSame(playerLight.spot, poltergeist.handLight);
            Assert.IsTrue(soul.Powers.Has(Power.Poltergeist));
            Assert.IsTrue(soul.Powers.Has(Power.Light));

            var objects = MovableObject.Active;
            Assert.AreEqual(4, objects.Count);
            foreach (ThrowableKind kind in System.Enum.GetValues(typeof(ThrowableKind)))
            {
                if (kind == ThrowableKind.Candle) continue;
                int count = 0;
                foreach (var movable in objects)
                    if (movable.kind == kind) count++;
                Assert.AreEqual(1, count, $"Debe haber un objeto de tipo {kind}.");
            }
            foreach (var movable in objects)
            {
                Assert.LessOrEqual(Vector3.Distance(player.transform.position, movable.transform.position), 12f);
                var body = movable.GetComponent<Rigidbody>();
                Assert.IsNotNull(body);
                Assert.IsTrue(body.isKinematic);
                Assert.IsNotNull(movable.sound);
                Assert.IsNotNull(movable.sound.clip);
                Assert.AreEqual(movable.kind.ToString().ToLowerInvariant(), movable.sound.clip.name);
                Assert.AreEqual(0f, movable.sound.spatialBlend);
            }

            float expectedEnergy = soul.Energy.Current - 15f;
            playerLight.Toggle();
            yield return null;
            yield return null;

            LogAssert.Expect(LogType.Log, new Regex(@"Poltergeist: \w+ suena a"));
            Assert.IsTrue(poltergeist.TryMove());
            Assert.AreEqual(expectedEnergy, soul.Energy.Current, 0.5f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainSceneHasTwoCandlesTiffanyCanKindle()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var kindler = player.GetComponent<CandleKindler>();
            var soul = player.GetComponent<PlayerSoul>();
            Assert.IsNotNull(kindler);
            Assert.IsNotNull(soul);
            Assert.IsTrue(soul.Powers.Has(Power.Candles));
            Assert.AreEqual(2, kindler.candles.Length);
            foreach (var candle in kindler.candles)
            {
                Assert.IsNotNull(candle);
                Assert.AreEqual(CandleKind.Kindled, candle.Candle.Kind);
                Assert.IsFalse(candle.Candle.IsLit);
                Assert.IsFalse(candle.startLit);
                Assert.IsNotNull(candle.flameMesh);
                Assert.IsFalse(candle.flameMesh.enabled);
            }

            float expectedEnergy = soul.Energy.Current - SoulCosts.LightCandle;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tiffany enciende una veladora");
            Assert.IsTrue(kindler.TryKindle());
            yield return null;

            int lit = 0;
            foreach (var candle in kindler.candles)
                if (candle.Candle.IsLit) lit++;
            Assert.AreEqual(1, lit);
            Assert.AreEqual(expectedEnergy, soul.Energy.Current, 0.5f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WalkingInMainScenePublishesStepsAndCameraFollows()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var viewCamera = Camera.main;
            Assert.IsNotNull(viewCamera);
            var initialPlayerPosition = player.transform.position;
            var initialCameraPosition = viewCamera.transform.position;

            GameInput.MoveOverride = Vector2.up;

            yield return new WaitForSeconds(0.5f);
            float followingDistance = HorizontalDistance(viewCamera.transform.position, player.transform.position);

            yield return new WaitForSeconds(1.5f);

            Assert.Greater(HorizontalDistance(player.transform.position, initialPlayerPosition), 2f);
            Assert.That(steps.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var step in steps) Assert.Greater(step.Radius, 0f);
            Assert.Greater(HorizontalDistance(viewCamera.transform.position, initialCameraPosition), 0f);
            Assert.That(HorizontalDistance(viewCamera.transform.position, player.transform.position),
                Is.EqualTo(followingDistance).Within(1f));
            LogAssert.NoUnexpectedReceived();
        }

        void RecordStep(StepNoise noise)
        {
            steps.Add(noise);
        }

        static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            var displacement = first - second;
            displacement.y = 0f;
            return displacement.magnitude;
        }
    }
}
