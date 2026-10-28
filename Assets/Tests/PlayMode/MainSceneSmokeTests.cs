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
            var footsteps = player.GetComponent<PlayerFootsteps>();
            Assert.IsNotNull(footsteps);
            Assert.IsNotNull(footsteps.source);
            Assert.AreEqual(0f, footsteps.source.spatialBlend);
            Assert.IsFalse(footsteps.source.playOnAwake);
            Assert.AreNotSame(player.GetComponent<Heartbeat>().source, footsteps.source);
            foreach (var clips in new[] { footsteps.dirt, footsteps.grass, footsteps.slab,
                footsteps.gravel, footsteps.leaves, footsteps.branches })
            {
                Assert.That(clips, Has.Length.EqualTo(4));
                Assert.That(clips, Has.All.Not.Null);
            }
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
        public IEnumerator RouteHideEntersAndLeavesThroughInteraction()
        {
            var hide = GameObject.Find("Route").transform.GetChild(3).GetComponentInChildren<HideVolume>();
            Assert.IsNotNull(hide);
            var hiding = Object.FindAnyObjectByType<PlayerHiding>();
            hiding.gameInterface.StartGame();
            var controller = hiding.GetComponent<CharacterController>();
            controller.enabled = false;
            hiding.transform.position = hide.HidePoint + Vector3.left * 1.2f;
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            Vector3 entry = hiding.transform.position;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsTrue(hiding.Hidden);
            Assert.Less(Vector3.Distance(hide.HidePoint, hiding.transform.position), 0.001f);
            yield return null;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(hiding.Hidden);
            Assert.Less(Vector3.Distance(entry, hiding.transform.position), 0.05f);
            Assert.IsTrue(hiding.GetComponent<PlayerController>().enabled && controller.enabled);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RouteSharesOneNavMeshAndStartsTheCastInTheCemetery()
        {
            var route = GameObject.Find("Route").transform;
            var cemetery = route.GetChild(0);
            Assert.AreEqual(8, route.childCount);
            Assert.AreEqual(55, Object.FindObjectsByType<CaptureStop>(FindObjectsSortMode.None).Length,
                "47 paradas de escena, una vista previa, cinco ecos y dos de la pelota.");
            Assert.AreEqual(19, route.GetComponentsInChildren<CaptureStop>().Length);
            Assert.AreEqual(6, Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(5, route.GetComponentsInChildren<HideVolume>().Length);
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
                Assert.AreEqual(route.GetChild(i).position + Vector3.up * 7.9f, witch.skyRoute[i]);

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
            foreach (var hide in route.GetComponentsInChildren<HideVolume>())
            {
                Assert.IsTrue(NavMesh.SamplePosition(hide.HidePoint, out var entry, 0.5f, NavMesh.AllAreas), hide.name);
                Assert.IsTrue(NavMesh.CalculatePath(from.position, entry.position, NavMesh.AllAreas, path), hide.name);
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, hide.name);
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
            Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include).enabled = false;
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var viewCamera = Camera.main;
            Assert.IsNotNull(viewCamera);
            var initialPlayerPosition = player.transform.position;
            var initialCameraPosition = viewCamera.transform.position;
            var footsteps = player.GetComponent<PlayerFootsteps>();
            int initialSteps = footsteps.Steps;

            GameInput.MoveOverride = Vector2.up;

            yield return new WaitForSeconds(0.5f);
            float followingDistance = HorizontalDistance(viewCamera.transform.position, player.transform.position);

            yield return new WaitForSeconds(1.5f);

            Assert.Greater(HorizontalDistance(player.transform.position, initialPlayerPosition), 2f);
            Assert.That(steps.Count, Is.GreaterThanOrEqualTo(3));
            Assert.AreEqual(steps.Count, footsteps.Steps - initialSteps);
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
