using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed class RouteBuilderTests
    {
        const string TempFolder = "Assets/RouteBuilderTestTemp";
        GameObject root;
        Transform route;
        PlayerController player;
        Storm storm;
        Vector3[] skyRoute;
        NavMeshData navMeshData;
        string previousMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            previousMaterialFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = TempFolder + "/Materials";
            root = new GameObject(nameof(RouteBuilderTests));
            var girl = new GameObject("Player");
            girl.transform.SetParent(root.transform, false);
            girl.AddComponent<PlayerSoul>();
            girl.AddComponent<PlayerHiding>();
            player = girl.AddComponent<PlayerController>();
            storm = new GameObject("Storm").AddComponent<Storm>();
            storm.transform.SetParent(root.transform, false);
            route = RouteBuilder.BuildRoute(root.transform, player, storm, out var memories, out skyRoute);
            var gameInterface = new GameObject("Interface", typeof(RectTransform)).AddComponent<GameInterface>();
            gameInterface.transform.SetParent(root.transform, false);
            MemoryBuilder.Build(root.transform, player, gameInterface, null, null, memories, TempFolder + "/EchoVolumeProfile.asset");
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            if (navMeshData != null) Object.DestroyImmediate(navMeshData);
            AssetDatabase.DeleteAsset(TempFolder);
            GeneratedMaterials.folder = previousMaterialFolder;
        }

        [Test]
        public void EightClearingsHaveOrderedCapturesGroundsAndWiredFamilyCandles()
        {
            var names = new[] { "Route 1 Cemetery", "Route 2 Water Plant", "Route 3 Train Tracks", "Route 4 Stable",
                "Route 5 Witch Hut", "Route 6 Lake", "Route 7 Road", "Route 8 Tiffany House" };
            var groundTypes = new[] { GroundType.Grass, GroundType.Slab, GroundType.Gravel, GroundType.Branches,
                GroundType.Leaves, GroundType.Grass, GroundType.Slab, GroundType.Dirt };
            Assert.AreEqual("Route", route.name);
            Assert.AreEqual(8, route.childCount);
            var captures = route.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(8, captures.Length);
            System.Array.Sort(captures, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < names.Length; i++)
            {
                var clearing = route.GetChild(i);
                Assert.AreEqual(names[i], clearing.name);
                Assert.AreEqual(names[i], captures[i].name);
                Assert.IsTrue(captures[i].transform.IsChildOf(clearing));
                Assert.IsFalse(captures[i].overrideCamera);
                Assert.AreEqual(1, clearing.GetComponentsInChildren<CaptureStop>().Length);
                var ground = clearing.GetComponentInChildren<GroundSurface>();
                Assert.AreEqual(groundTypes[i], ground.type);
                Assert.AreEqual(36f, ground.GetComponent<Collider>().bounds.size.z);
                Assert.GreaterOrEqual(clearing.position.x - 18f, 90f);
                var candles = clearing.GetComponentsInChildren<CandleSpot>();
                Assert.AreEqual(1, candles.Length);
                Assert.AreEqual(CandleKind.Family, candles[0].kind);
                Assert.AreSame(player.GetComponent<PlayerSoul>(), candles[0].player);
                Assert.AreSame(storm, candles[0].storm);
                Assert.IsNotNull(candles[0].flame);
                Assert.IsTrue(candles[0].flame.transform.IsChildOf(candles[0].transform));
            }
            Assert.IsNotNull(route.GetChild(7).Find("Window Candle"));
        }

        [Test]
        public void FiveMemoriesBelongToTheirClearingsAndEchoesStayOutsideTheRoute()
        {
            var collector = player.GetComponent<MemoryCollector>();
            Assert.AreEqual(6, collector.spots.Length);
            var echoes = root.transform.Find("Memory Echoes");
            Assert.IsNotNull(echoes);
            Assert.IsFalse(echoes.IsChildOf(route));
            Assert.AreEqual(5, echoes.childCount);
            for (int i = 0; i < 8; i++)
            {
                var spots = route.GetChild(i).GetComponentsInChildren<MemorySpot>();
                Assert.AreEqual(i == 0 ? 2 : i < 5 ? 1 : 0, spots.Length);
                if (i >= 5) continue;
                Assert.AreEqual(i + 1, spots[0].memoryId);
                Assert.AreEqual(MemorySpotKind.Memory, spots[0].kind);
                Assert.AreSame(spots[0], collector.spots[i == 0 ? 0 : i + 1]);
                if (i == 0) Assert.AreSame(spots[1], collector.spots[1]);
                var echo = spots[0].echoPlayer;
                Assert.IsNotNull(echo);
                Assert.AreSame(echoes, echo.stageRoot.transform.parent);
                Assert.Less(Vector3.Distance(spots[0].transform.position, echo.stageRoot.transform.position), 0.001f);
                Assert.Less(Quaternion.Angle(spots[0].transform.rotation, echo.stageRoot.transform.rotation), 0.001f);
                Assert.IsFalse(echo.stageRoot.activeSelf);
                Assert.AreEqual(Vector3.up, spots[0].transform.up);
                Assert.IsNotNull(echo.ground);
                Assert.IsTrue(echo.ground.transform.IsChildOf(route.GetChild(i)));
            }
            var grave = route.GetChild(0).Find("Tiffany Gravestone");
            Assert.Less(Vector3.Distance(grave.position, collector.spots[0].transform.position), 2f);
            var ball = route.GetChild(0).Find("Ball");
            Assert.IsNotNull(ball);
            Assert.IsNull(ball.GetComponentInChildren<MemorySpot>());
            Assert.AreEqual(MemorySpotKind.Ball, collector.spots[1].kind);
            Assert.AreEqual(MemoryProgress.BallId, collector.spots[1].memoryId);
            Assert.AreSame(ball.gameObject, collector.spots[1].visual);
            Assert.AreEqual(ball.position, collector.spots[1].transform.position);
            Assert.IsNull(collector.spots[1].echoPlayer);
            var stages = root.GetComponentsInChildren<MemoryCaptureStage>();
            Assert.AreEqual(3, stages.Length);
            CollectionAssert.AreEquivalent(new[] { "Memory 0 Spot", "Ball 0 Before", "Ball 1 Tyler" },
                System.Array.ConvertAll(stages, stage => stage.name));
            foreach (var stage in stages)
            {
                bool isBall = stage.name.StartsWith("Ball ");
                Assert.AreSame(collector, stage.collector);
                Assert.AreSame(collector.spots[isBall ? 1 : 0], stage.spot);
                Assert.AreEqual(stage.name == "Ball 1 Tyler", stage.collect);
                Assert.IsFalse(stage.transform.IsChildOf(route));
                Assert.Less(Vector3.Distance(stage.transform.position, stage.spot.transform.position), 2f);
                var capture = stage.GetComponent<CaptureStop>();
                Assert.IsTrue(capture.overrideCamera);
                Vector3 focus = isBall ? stage.transform.position : stage.spot.transform.position;
                Vector3 toSpot = (focus - capture.cameraPosition).normalized;
                Assert.Greater(Vector3.Dot(capture.cameraRotation * Vector3.forward, toSpot), 0.99f);
            }
            var echoStages = root.GetComponentsInChildren<EchoCaptureStage>();
            Assert.AreEqual(5, echoStages.Length);
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (var stage in echoStages)
            {
                Assert.IsTrue(ids.Add(stage.spot.memoryId));
                Assert.AreSame(collector, stage.collector);
                Assert.AreSame(System.Array.Find(collector.spots, spot => spot.memoryId == stage.spot.memoryId), stage.spot);
                Assert.AreEqual(MemorySpotKind.Memory, stage.spot.kind);
                Assert.Less(Vector3.Distance(stage.transform.position, stage.spot.transform.position), 6f);
                Assert.IsTrue(stage.spot.echoPlayer.ground.Raycast(
                    new Ray(stage.transform.position + Vector3.up * 3f, Vector3.down), out var hit, 6f));
                Assert.AreEqual(hit.point.y, stage.transform.position.y, 0.001f);
                var capture = stage.GetComponent<CaptureStop>();
                Assert.IsTrue(capture.overrideCamera);
                Vector3 focus = stage.spot.transform.TransformPoint(new Vector3(0f, 0.7f, 0.8f));
                Assert.Greater(Vector3.Dot(capture.cameraRotation * Vector3.forward,
                    (focus - capture.cameraPosition).normalized), 0.999f);
            }
        }

        [Test]
        public void PlayerStartsInsideCemeteryFacingTheGravestone()
        {
            var cemetery = route.GetChild(0);
            Vector3 position = cemetery.InverseTransformPoint(player.transform.position);
            Assert.That(position.x, Is.InRange(-17f, 17f));
            Assert.That(position.z, Is.InRange(-17f, -10f));
            Assert.AreEqual(0f, position.y, 1e-4f);
            Assert.AreEqual(cemetery.Find("Spawn").position, player.transform.position);
            Vector3 toGrave = cemetery.Find("Tiffany Gravestone").position - player.transform.position;
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.ProjectOnPlane(toGrave, Vector3.up).normalized), 0.99f);
        }

        [Test]
        public void FlightRouteVisitsEveryClearingAtCruiseHeightInOrder()
        {
            Assert.Greater(RouteBuilder.SkyHeight, 7f + 0.5f);
            Assert.AreEqual(8, skyRoute.Length);
            for (int i = 0; i < skyRoute.Length; i++)
            {
                Vector3 offset = skyRoute[i] - route.GetChild(i).position;
                Assert.AreEqual(Vector3.up * RouteBuilder.SkyHeight, offset);
            }
        }

        [Test]
        public void FirstFiveClearingsHaveHidesReachableFromThePathOnBakedNavMesh()
        {
            var surface = route.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            navMeshData = surface.navMeshData;
            Assert.AreEqual(5, route.GetComponentsInChildren<HideVolume>().Length);
            var path = new NavMeshPath();
            for (int i = 0; i < 5; i++)
            {
                var clearing = route.GetChild(i);
                var hides = clearing.GetComponentsInChildren<HideVolume>();
                Assert.AreEqual(1, hides.Length, clearing.name);
                var hide = hides[0];
                Assert.IsTrue(hide.GetComponent<Collider>().isTrigger, hide.name);
                Assert.AreEqual(LayerMask.NameToLayer("Ignore Raycast"), hide.gameObject.layer);
                Assert.IsTrue(NavMesh.SamplePosition(clearing.TransformPoint(new Vector3(0f, 0f, -13f)),
                    out var start, 0.5f, NavMesh.AllAreas), clearing.name);
                foreach (var entry in new[] { hide.HidePoint, hide.HidePoint + Vector3.left * 1.2f })
                {
                    Assert.IsTrue(NavMesh.SamplePosition(entry, out var end, 0.5f, NavMesh.AllAreas), hide.name);
                    Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), hide.name);
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, hide.name);
                }
            }
        }

        [Test]
        public void RouteUsesFewStaticRenderersSavedUrpMaterialsAndOnlyCandleLights()
        {
            Assert.Less(route.GetComponentsInChildren<Transform>(true).Length, 600);
            var renderers = route.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            Assert.Less(renderers.Length, 300);
            Assert.AreEqual(4, System.Array.FindAll(renderers,
                renderer => renderer.GetComponentInParent<HideVolume>() != null).Length);
            foreach (var renderer in renderers)
            {
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic), renderer.name);
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, renderer.name);
                Assert.IsNotEmpty(renderer.sharedMaterials, renderer.name);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name);
                    Assert.IsTrue(AssetDatabase.Contains(material), renderer.name);
                    Assert.IsNotNull(material.shader, renderer.name);
                    StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                    StringAssert.DoesNotContain("Error", material.shader.name);
                }
            }
            var graveMaterial = route.GetChild(0).Find("Tiffany Gravestone").GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.AreEqual("Assets/Art/Kenney/Graveyard/Models/Textures/colormap.png", AssetDatabase.GetAssetPath(graveMaterial.GetTexture("_BaseMap")));
            var lights = route.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(8, lights.Length);
            foreach (var light in lights)
            {
                Assert.IsNotNull(light.GetComponentInParent<CandleSpot>());
                Assert.AreEqual(LightShadows.None, light.shadows);
            }
        }

        [Test]
        public void PathsJoinClearingsWithFiveMeterOpeningsAndClosedOuterEdges()
        {
            for (int i = 0; i < 8; i++)
            {
                var clearing = route.GetChild(i);
                Assert.IsTrue(HitsRoute(clearing.position + Vector3.up, Vector3.left, 19f));
                Assert.IsTrue(HitsRoute(clearing.position + Vector3.up, Vector3.right, 19f));
                if (i == 7) continue;
                var path = clearing.Find("Path to Next Clearing/Ground").GetComponent<Collider>().bounds;
                Assert.AreEqual(5f, path.size.x);
                Assert.AreEqual(clearing.position.z + 18f, path.min.z);
                Assert.AreEqual(route.GetChild(i + 1).position.z - 18f, path.max.z);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 exit = clearing.position + new Vector3(side * 2f, 1f, 17f);
                    Vector3 entrance = route.GetChild(i + 1).position + new Vector3(side * 2f, 1f, -19f);
                    Assert.IsFalse(HitsRoute(exit, Vector3.forward, 2f));
                    Assert.IsFalse(HitsRoute(entrance, Vector3.forward, 2f));
                }
            }
            Assert.IsTrue(HitsRoute(route.GetChild(0).position + new Vector3(0f, 1f, -17f), Vector3.back, 2f));
            Assert.IsTrue(HitsRoute(route.GetChild(7).position + new Vector3(0f, 1f, 17f), Vector3.forward, 2f));
        }

        [Test]
        public void LakeHasNoWalkableWaterAndItsBanksBlockEntry()
        {
            var lake = route.GetChild(5);
            var water = lake.Find("Water");
            Assert.IsNull(water.GetComponent<Collider>());
            Assert.IsFalse(HitsRoute(water.position + Vector3.up * 2f, Vector3.down, 4f));
            Assert.IsTrue(HitsRoute(lake.TransformPoint(new Vector3(0f, 1f, 3f)), Vector3.right, 9f));
            Assert.IsTrue(HitsRoute(lake.TransformPoint(new Vector3(17f, 1f, 3f)), Vector3.left, 8f));
            Assert.IsTrue(HitsRoute(lake.TransformPoint(new Vector3(9f, 1f, -12f)), Vector3.forward, 10f));
            Assert.IsTrue(HitsRoute(lake.TransformPoint(new Vector3(9f, 1f, 17f)), Vector3.back, 8f));
        }

        [Test]
        public void RoofsTanksAndAllElevatedCollidersAreNotWalkable()
        {
            int roofCount = 0;
            int tankCount = 0;
            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            Assert.GreaterOrEqual(notWalkable, 0);
            foreach (var collider in route.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.bounds.max.y <= route.position.y + 1f) continue;
                var modifier = collider.GetComponent<NavMeshModifier>();
                Assert.IsNotNull(modifier, collider.name);
                Assert.IsTrue(modifier.isActiveAndEnabled, collider.name);
                Assert.IsTrue(modifier.overrideArea, collider.name);
                Assert.AreEqual(notWalkable, modifier.area, collider.name);
                Assert.IsTrue(modifier.AffectsAgentType(NavMesh.GetSettingsByIndex(0).agentTypeID), collider.name);
                Assert.IsFalse(modifier.ignoreFromBuild, collider.name);
                if (collider.name == "Roof") roofCount++;
                if (collider.name.StartsWith("Tank ")) tankCount++;
            }
            Assert.AreEqual(3, roofCount);
            Assert.AreEqual(2, tankCount);
            foreach (var ground in route.GetComponentsInChildren<GroundSurface>())
                Assert.IsNull(ground.GetComponentInParent<NavMeshModifier>(), ground.name);
        }

        [TestCase(3, "Barn", 5f)]
        [TestCase(4, "Hut", 3.5f)]
        [TestCase(7, "House", 4f)]
        public void SheltersKeepFullWallCollisionsAndLowCameraFacingWalls(int index, string name, float height)
        {
            var shelter = route.GetChild(index).Find(name);
            var roof = shelter.Find("Roof");
            Assert.IsFalse(roof.GetComponent<Renderer>().enabled);
            Assert.IsTrue(roof.GetComponent<Collider>().enabled);
            foreach (string wallName in new[] { "Side Wall -1", "Front Wall -1", "Front Wall 1" })
            {
                var wall = shelter.Find(wallName);
                var renderer = wall.GetComponent<Renderer>();
                var collider = wall.GetComponent<Collider>();
                Assert.IsTrue(renderer.enabled, wallName);
                Assert.LessOrEqual(renderer.bounds.max.y - shelter.position.y, 1f, wallName);
                Assert.AreEqual(shelter.position.y, collider.bounds.min.y, 1e-4f, wallName);
                Assert.AreEqual(shelter.position.y + height, collider.bounds.max.y, 1e-4f, wallName);
                var normal = wallName.StartsWith("Side") ? Vector3.right : Vector3.forward;
                foreach (float y in new[] { 0.6f, height - 0.25f })
                {
                    var origin = collider.bounds.center + normal * 2f;
                    origin.y = shelter.position.y + y;
                    Assert.IsTrue(collider.Raycast(new Ray(origin, -normal), out _, 4f), wallName);
                }
            }
            float depth = shelter.Find("Side Wall 1").GetComponent<Collider>().bounds.size.z;
            Assert.IsFalse(HitsRoute(shelter.position + Vector3.up * 0.6f, Vector3.back, depth * 0.5f + 1f));
        }

        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(7, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        public void FollowCameraSeesPlayerNearMemoriesAndHides(int index, bool atHide)
        {
            var clearing = route.GetChild(index);
            var spot = atHide ? null : clearing.GetComponentInChildren<MemorySpot>();
            Vector3 position = atHide ? clearing.GetComponentInChildren<HideVolume>().HidePoint + Vector3.left * 1.2f
                : spot != null ? spot.transform.position : clearing.TransformPoint(new Vector3(0f, 0f, 3f));
            Vector3 marker = spot != null ? spot.visual.GetComponent<Renderer>().bounds.center : position + Vector3.up * 0.18f;
            var camera = new GameObject("Follow Camera").AddComponent<FollowCamera>();
            camera.transform.SetParent(root.transform, false);
            camera.target = player.transform;
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                camera.Snap();
                foreach (var target in new[] { position + Vector3.up * 0.6f, marker })
                {
                    Vector3 direction = target - camera.transform.position;
                    var ray = new Ray(camera.transform.position, direction.normalized);
                    foreach (var renderer in route.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.enabled || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                        if (spot != null && renderer.transform.IsChildOf(spot.transform)) continue;
                        Assert.IsFalse(renderer.bounds.IntersectRay(ray, out float distance) && distance < direction.magnitude,
                            $"{clearing.name}: {renderer.name}, yaw {yaw}, objetivo {target}");
                    }
                }
            }
        }

        [Test]
        public void CemeteryCaptureWaitsUntilThePreviousMemoryEchoHasEnded()
        {
            var echo = new MemoryEcho();
            var stage = root.GetComponentInChildren<EchoCaptureStage>();
            Assert.IsTrue(EchoData.TryLoad(stage.spot.echoPlayer.echoAsset.text, out var data, out _));
            echo.Start(data.duration);
            echo.Tick(stage.GetComponent<CaptureStop>().wait);
            Assert.IsTrue(echo.Active);
            Assert.Greater(echo.Weight, 0f);
            echo.Tick(route.GetChild(0).GetComponentInChildren<CaptureStop>().wait);
            Assert.IsFalse(echo.Active);
            Assert.AreEqual(0f, echo.Weight);
        }

        bool HitsRoute(Vector3 origin, Vector3 direction, float distance)
        {
            foreach (var collider in route.GetComponentsInChildren<Collider>())
                if (!collider.isTrigger && collider.Raycast(new Ray(origin, direction), out _, distance)) return true;
            return false;
        }
    }
}
