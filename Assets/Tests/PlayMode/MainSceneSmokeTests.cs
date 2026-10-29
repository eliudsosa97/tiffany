using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public class MainSceneSmokeTests : TiffanyLogTests
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
        public IEnumerator MainSceneHasSilhouettesOnlyOnTiffanyAndTyler()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var tyler = Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            var light = player.GetComponent<PlayerLight>();
            foreach (var figure in new[] { player.transform, tyler.figure.transform })
            {
                var renderers = figure.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers);
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    if (renderer is ParticleSystemRenderer || renderer.transform.IsChildOf(light.glow)
                        || renderer.GetComponentInParent<Light>(true) != null)
                    {
                        foreach (var material in materials)
                        {
                            Assert.AreNotEqual("Universal Render Pipeline/Tiffany Silhouette", material.shader.name, renderer.name);
                            Assert.IsFalse(material.IsKeywordEnabled("_TIFFANY_CHARACTER"), renderer.name);
                        }
                        continue;
                    }
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(Mathf.Max(2, mesh.subMeshCount), materials.Length, renderer.name);
                    for (int i = 0; i < materials.Length - 1; i++)
                    {
                        Assert.AreEqual(1f, materials[i].GetFloat("_TiffanyStencil"), renderer.name);
                        if (materials[i].shader.name != "TiffanyGame/Spirit")
                            Assert.IsTrue(materials[i].IsKeywordEnabled("_TIFFANY_CHARACTER"), renderer.name);
                    }
                    Assert.AreEqual("Universal Render Pipeline/Tiffany Silhouette", materials[materials.Length - 1].shader.name, renderer.name);
                    Assert.AreEqual(1, materials[materials.Length - 1].passCount);
                }
            }
            var witch = Object.FindAnyObjectByType<WitchController>();
            foreach (var renderer in witch.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.AreNotEqual("Universal Render Pipeline/Tiffany Silhouette", material.shader.name, renderer.name);
            foreach (string name in new[] { "Cover 1 Tree", "Cover 2 Building", "Cover 3 Dense Forest" })
                foreach (string suffix in new[] { " Before", " After" })
                {
                    var stop = GameObject.Find(name + suffix)?.GetComponent<CaptureStop>();
                    Assert.IsNotNull(stop, name + suffix);
                    Assert.AreEqual(suffix == " After", stop.GetComponent<OcclusionCaptureStage>().after);
                }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CoverCapturesBlockTiffanyFromTheSettledGameplayCameraAndToggleTheEffect()
        {
            Object.FindAnyObjectByType<GameInterface>().StartGame();
            yield return null;
            var player = GameObject.FindGameObjectWithTag("Player").transform;
            var controller = player.GetComponent<CharacterController>();
            var follow = Camera.main.GetComponent<FollowCamera>();
            var camera = follow.GetComponent<Camera>();
            var stages = Object.FindObjectsByType<OcclusionCaptureStage>(FindObjectsSortMode.None);
            var stops = System.Array.ConvertAll(stages, stage => stage.GetComponent<CaptureStop>());
            System.Array.Sort(stops, AutoCapture.CompareStops);
            Assert.AreEqual(6, stops.Length);
            follow.enabled = false;
            follow.transform.position = Vector3.one * 500f;
            Vector3 beforePlayer = default, beforeCamera = default;
            Quaternion beforeRotation = default;
            float previousDisabled = Shader.GetGlobalFloat("_TiffanyOcclusionDisabled");
            for (int i = 0; i < stops.Length; i++)
            {
                var stop = stops[i];
                var stage = stop.GetComponent<OcclusionCaptureStage>();
                try
                {
                    yield return stage.Prepare(player, follow);
                    var settled = follow.transform.position;
                    yield return new WaitForSecondsRealtime(stop.wait);
                    yield return null;
                    Assert.AreEqual(i % 2 == 1, stage.after, stop.name);
                    Assert.IsFalse(stop.overrideCamera, stop.name);
                    Assert.IsFalse(follow.enabled, stop.name);
                    Assert.Less(Vector3.Distance(settled, follow.transform.position), 1e-4f, stop.name);
                    Assert.Less(Vector3.Distance(stop.transform.position, player.position), 1e-3f, stop.name);
                    Assert.Less(Vector3.Distance(stop.cameraPosition, follow.transform.position), 1e-3f, stop.name);
                    Assert.Less(Quaternion.Angle(stop.cameraRotation, follow.transform.rotation), 0.01f, stop.name);
                    Assert.AreEqual(follow.fieldOfView, camera.fieldOfView);
                    var head = player.TransformPoint(controller.center + Vector3.up * controller.height * 0.5f);
                    var viewport = camera.WorldToViewportPoint(head);
                    Assert.That(viewport.x, Is.InRange(0f, 1f), stop.name);
                    Assert.That(viewport.y, Is.InRange(0f, 1f), stop.name);
                    Assert.Greater(viewport.z, camera.nearClipPlane, stop.name);
                    var ray = new Ray(camera.transform.position, head - camera.transform.position);
                    float distance = Vector3.Distance(head, camera.transform.position) - FollowCamera.OcclusionFocusMargin;
                    Assert.GreaterOrEqual(stage.covers.Length, stop.name.StartsWith("Cover 3") ? 2 : 1, stop.name);
                    foreach (var cover in stage.covers)
                        Assert.IsTrue(cover.collider != null ? cover.collider.Raycast(ray, out _, distance)
                            : cover.bounds.IntersectRay(ray, out float entry) && entry > 0f && entry < distance, stop.name);
                    Assert.AreEqual(stage.after ? 0f : 1f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"), stop.name);
                    Assert.AreEqual(FollowCamera.OcclusionRadius, Shader.GetGlobalVector("_TiffanyFocusWS").w);
                    Assert.Less(Vector3.Distance(player.position + Vector3.up * FollowCamera.ChestHeight,
                        Shader.GetGlobalVector("_TiffanyFocusWS")), 1e-4f, stop.name);
                    Assert.Less(Vector3.Distance(camera.transform.position,
                        Shader.GetGlobalVector("_TiffanyCameraWS")), 1e-4f, stop.name);
                    if (!stage.after)
                    {
                        beforePlayer = player.position;
                        beforeCamera = camera.transform.position;
                        beforeRotation = camera.transform.rotation;
                    }
                    else
                    {
                        Assert.AreEqual(beforePlayer, player.position);
                        Assert.AreEqual(beforeCamera, camera.transform.position);
                        Assert.AreEqual(beforeRotation, camera.transform.rotation);
                    }
                }
                finally { stage.Clear(); }
                Assert.AreEqual(previousDisabled, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsTrue(player.GetComponent<PlayerController>().enabled);
            }
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
            var save = player.GetComponent<SaveGame>();
            Assert.IsNotNull(save);
            Assert.AreSame(soul, save.soul);
            Assert.AreSame(witch, save.witch);
            var family = System.Array.FindAll(Object.FindObjectsByType<CandleSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                candle => candle.kind == CandleKind.Family);
            Assert.AreEqual(14, family.Length);
            CollectionAssert.AreEquivalent(family, save.candles);
            var ids = new HashSet<string>();
            foreach (var candle in family)
            {
                Assert.IsFalse(string.IsNullOrEmpty(candle.saveId), candle.name);
                Assert.IsTrue(ids.Add(candle.saveId), candle.saveId);
                Assert.AreEqual(candle.transform.parent.name + "/" + candle.name, candle.saveId);
            }
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
            Assert.IsFalse(soul.Powers.Has(Power.Poltergeist));
            Assert.IsTrue(soul.Powers.Has(Power.Light));

            var objects = MovableObject.Active;
            Assert.AreEqual(6, objects.Count, "Cuatro objetos originales y dos campanas de los retos.");
            foreach (ThrowableKind kind in System.Enum.GetValues(typeof(ThrowableKind)))
            {
                if (kind == ThrowableKind.Candle) continue;
                int count = 0;
                foreach (var movable in objects)
                    if (movable.kind == kind) count++;
                Assert.AreEqual(kind == ThrowableKind.Bell ? 3 : 1, count, $"Objetos de tipo {kind}.");
            }
            foreach (var movable in objects)
            {
                if (movable.GetComponentInParent<PowerChallenge>() == null)
                    Assert.LessOrEqual(Vector3.Distance(player.transform.position, movable.transform.position), 12f);
                var body = movable.GetComponent<Rigidbody>();
                Assert.IsNotNull(body);
                Assert.IsTrue(body.isKinematic);
                Assert.IsNotNull(movable.sound);
                Assert.IsNotNull(movable.sound.clip);
                Assert.AreEqual(movable.kind.ToString().ToLowerInvariant(), movable.sound.clip.name);
                Assert.AreEqual(0f, movable.sound.spatialBlend);
            }

            soul.Powers.Unlock(Power.Poltergeist);
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
            Assert.AreEqual(RouteLayout.Count + 1, route.childCount);
            Assert.IsNotNull(route.Find("Landscape/Terrain"));
            Assert.AreEqual(4, Object.FindAnyObjectByType<MemoryCollector>().challenges.Length);
            Assert.AreEqual(79, Object.FindObjectsByType<CaptureStop>(FindObjectsSortMode.None).Length,
                "58 paradas de main (48 de escena, una vista previa, cinco ecos, dos de la pelota " +
                "y dos vistas del agua), Witch Suspicious, seis comparaciones de neblina, ocho vistas de los retos y seis de cobertura.");
            Assert.AreEqual(29, route.GetComponentsInChildren<CaptureStop>().Length, "21 paradas previas y ocho vistas de los retos.");
            Assert.AreEqual(6, Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(5, route.GetComponentsInChildren<HideVolume>().Length);
            Assert.AreEqual(20, Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None).Length,
                "Doce originales, cuatro de recarga y cuatro para encender en los retos.");
            foreach (var movable in MovableObject.Active)
                Assert.IsTrue(movable.transform.IsChildOf(cemetery) || movable.GetComponentInParent<PowerChallenge>() != null);
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
            Assert.GreaterOrEqual(witch.skyRoute.Length, 8);
            int cursor = 0;
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(RouteLayout.Clearing(i), route.GetChild(i).localPosition);
                Vector3 expected = route.GetChild(i).position + Vector3.up * 7.9f;
                int index = System.Array.FindIndex(witch.skyRoute, cursor, point => Vector3.Distance(point, expected) < 0.001f);
                Assert.GreaterOrEqual(index, cursor);
                cursor = index + 1;
            }

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
            var river = route.GetChild(5).Find("Path to Next Clearing/River Crossing/River");
            foreach (float side in new[] { -1.8f, 1.8f })
                Assert.IsFalse(NavMesh.SamplePosition(river.TransformPoint(new Vector3(side, 0f, 0f)), out _, 0.25f, NavMesh.AllAreas));
            var logBounds = river.Find("Log").GetComponent<Collider>().bounds;
            var logTop = new Vector3(logBounds.center.x, logBounds.max.y, logBounds.center.z);
            // El NavMesh sigue la superficie elevada del tronco, no el origen del río.
            Assert.IsTrue(NavMesh.SamplePosition(logTop, out var crossing, 0.3f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.CalculatePath(from.position, crossing.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainSceneHasSixCandlesUnlockedByTheSecondMemory()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var kindler = player.GetComponent<CandleKindler>();
            var soul = player.GetComponent<PlayerSoul>();
            Assert.IsNotNull(kindler);
            Assert.IsNotNull(soul);
            Assert.IsFalse(soul.Powers.Has(Power.Candles));
            Assert.IsFalse(kindler.TryKindle());
            soul.Powers.Unlock(Power.Candles);
            Assert.AreEqual(6, kindler.candles.Length, "Dos originales y dos en cada tramo oscuro.");
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

        [UnityTest]
        public IEnumerator TiffanyWalksAcrossRiverBothWaysWithoutFallingAndPublishesGravelAndBranches()
        {
            var river = PrepareRiverWalk(out var player);
            var body = player.GetComponent<CharacterController>();
            float minimumHeight = river.Find("Stone 1").GetComponent<Collider>().bounds.max.y - 0.15f;
            foreach (int direction in new[] { 1, -1 })
            {
                GameInput.MoveOverride = Vector2.zero;
                body.enabled = false;
                player.transform.SetPositionAndRotation(river.TransformPoint(new Vector3(0f, 0.03f, -3f * direction)),
                    Quaternion.LookRotation(river.forward * direction));
                body.enabled = true;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.15f);
                steps.Clear();
                GameInput.MoveOverride = Vector2.up * direction;
                float deadline = Time.time + 5f;
                while (river.InverseTransformPoint(player.transform.position).z * direction < 3f && Time.time < deadline)
                {
                    yield return null;
                    Assert.GreaterOrEqual(player.transform.position.y, minimumHeight, "Tiffany cayó por debajo del cruce.");
                    Assert.AreEqual(Gait.Walk, player.CurrentGait);
                }
                GameInput.MoveOverride = Vector2.zero;
                Assert.GreaterOrEqual(river.InverseTransformPoint(player.transform.position).z * direction, 3f,
                    "Tiffany no llegó a la otra orilla caminando.");
                bool gravel = false;
                bool branches = false;
                foreach (var step in steps)
                {
                    if (Mathf.Abs(river.InverseTransformPoint(step.Position).z) > RouteTerrain.RiverWidth * 0.5f) continue;
                    gravel |= Mathf.Approximately(step.Radius, NoiseModel.GravelRadius);
                    branches |= Mathf.Approximately(step.Radius, NoiseModel.BranchesRadius);
                }
                Assert.IsTrue(gravel, "NoiseBus no publicó pasos de grava sobre el río.");
                Assert.IsTrue(branches, "NoiseBus no publicó pasos de ramas sobre el tronco.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TiffanyCannotWalkIntoRiverOutsideTheCrossing()
        {
            var river = PrepareRiverWalk(out var player);
            var body = player.GetComponent<CharacterController>();
            foreach (float side in new[] { -1.8f, 1.8f })
            {
                GameInput.MoveOverride = Vector2.zero;
                body.enabled = false;
                player.transform.SetPositionAndRotation(river.TransformPoint(new Vector3(side, 0.03f, -3f)), river.rotation);
                body.enabled = true;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.15f);
                GameInput.MoveOverride = Vector2.up;
                yield return new WaitForSeconds(1.5f);
                var position = river.InverseTransformPoint(player.transform.position);
                Assert.That(position.z, Is.InRange(-2.5f, -2f), "El agua debe detener a Tiffany antes de entrar.");
                Assert.AreEqual(side, position.x, 0.05f);
                Assert.GreaterOrEqual(position.y, -0.03f);
            }
            GameInput.MoveOverride = Vector2.zero;
            LogAssert.NoUnexpectedReceived();
        }

        static Transform PrepareRiverWalk(out PlayerController player)
        {
            Object.FindAnyObjectByType<WitchController>(FindObjectsInactive.Include).gameObject.SetActive(false);
            Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include).gameObject.SetActive(false);
            Object.FindAnyObjectByType<Storm>().raining = false;
            player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
            var follow = player.viewCamera.GetComponent<FollowCamera>();
            follow.enabled = false;
            var river = GameObject.Find("Route").transform.GetChild(5).Find("Path to Next Clearing/River Crossing/River");
            player.viewCamera.transform.rotation = Quaternion.Euler(45f, river.eulerAngles.y, 0f);
            return river;
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
