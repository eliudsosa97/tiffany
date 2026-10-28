using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
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

            // Heartbeat registra cada cambio de ritmo y estos tests no admiten logs inesperados.
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
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
        public IEnumerator MainSceneHasRespawnAtCemeterySpawn()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var witch = Object.FindAnyObjectByType<WitchController>();
            Assert.IsNotNull(witch);
            var respawn = player.GetComponent<PlayerRespawn>();
            var soul = player.GetComponent<PlayerSoul>();
            Assert.IsNotNull(respawn);
            Assert.IsNotNull(soul);
            Assert.AreSame(witch, respawn.witch);
            Assert.IsNotNull(respawn.overlay);
            Assert.IsFalse(respawn.overlay.gameObject.activeSelf);
            var spawn = GameObject.Find("Route").transform.GetChild(0).Find("Spawn");
            Assert.IsNotNull(spawn);
            Assert.Less(Vector3.Distance(spawn.position, soul.Respawn.Position), 0.01f);

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainSceneHasLitFamilyCandlesRegisteredAsSafeZones()
        {
            yield return null;

            var candles = Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None);
            int familyCount = 0;
            foreach (var candle in candles)
            {
                if (candle.kind != CandleKind.Family) continue;
                familyCount++;
                Assert.IsTrue(candle.Candle.IsLit, candle.name);
                Assert.IsTrue(SafeZones.Contains(candle.transform.position), candle.name);
            }
            Assert.GreaterOrEqual(familyCount, 2);
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
        }

        [UnityTest]
        public IEnumerator RouteSharesOneNavMeshAndStartsTheCastInTheCemetery()
        {
            var route = GameObject.Find("Route").transform;
            var cemetery = route.GetChild(0);
            Assert.AreEqual(8, route.childCount);
            Assert.AreEqual(50, Object.FindObjectsByType<CaptureStop>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(19, route.GetComponentsInChildren<CaptureStop>().Length);
            Assert.AreEqual(12, Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None).Length);
            foreach (var movable in MovableObject.Active) Assert.IsTrue(movable.transform.IsChildOf(cemetery));
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector3 start = cemetery.InverseTransformPoint(player.transform.position);
            Assert.That(start.x, Is.InRange(-17f, 17f));
            Assert.That(start.z, Is.InRange(-17f, -10f));
            var witch = Object.FindAnyObjectByType<WitchController>();
            var tyler = Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            Assert.IsNotNull(tyler);
            Assert.IsFalse(tyler.gameObject.activeSelf);
            Assert.IsFalse(tyler.NearPlayer);
            Assert.IsTrue(witch.transform.IsChildOf(cemetery));
            Assert.IsTrue(tyler.transform.IsChildOf(cemetery));
            Assert.Less(Vector3.Distance(tyler.transform.position, cemetery.Find("Ball").position), 3f);
            Assert.AreEqual(8, witch.skyRoute.Length);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(route.GetChild(i).position + Vector3.up * 14f, witch.skyRoute[i]);

            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            Assert.AreEqual(1, surfaces.Length);
            Assert.AreEqual(CollectObjects.Children, surfaces[0].collectObjects);
            Assert.IsNotNull(surfaces[0].navMeshData);
            Assert.IsTrue(route.IsChildOf(surfaces[0].transform));
            var testZone = GameObject.Find("Test Zone").transform;
            Assert.IsTrue(testZone.IsChildOf(surfaces[0].transform));
            Assert.AreEqual(Vector3.zero, testZone.position);
            Assert.IsNotNull(testZone.Find("Hide Test"));
            Assert.IsNotNull(testZone.Find("Light 1 On"));
            Assert.AreEqual(2, testZone.GetComponentsInChildren<CandleSpot>().Length);
            Assert.IsTrue(NavMesh.SamplePosition(testZone.Find("Spawn").position, out _, 1f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out var from, 1f, NavMesh.AllAreas));
            var path = new NavMeshPath();
            for (int i = 1; i < 8; i++)
            {
                Assert.IsTrue(NavMesh.SamplePosition(route.GetChild(i).position, out var to, 1f, NavMesh.AllAreas));
                Assert.IsTrue(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, route.GetChild(i).name);
            }
            Assert.IsFalse(NavMesh.SamplePosition(route.GetChild(5).Find("Water").position, out _, 1f, NavMesh.AllAreas));
            yield return null;
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
