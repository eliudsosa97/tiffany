using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
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
            FogBuilder.Build(root.transform, route.GetComponent<WitchChapterTracker>());
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
            Assert.AreEqual(RouteLayout.Count + 1, route.childCount);
            Assert.IsNotNull(route.Find("Landscape/Terrain"));
            var captures = route.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(10, captures.Length, "Ocho claros y dos vistas del agua.");
            captures = System.Array.FindAll(captures, capture => capture.name.StartsWith("Route "));
            System.Array.Sort(captures, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < names.Length; i++)
            {
                var clearing = route.GetChild(i);
                Assert.AreEqual(names[i], clearing.name);
                Assert.AreEqual(names[i], captures[i].name);
                Assert.IsTrue(captures[i].transform.IsChildOf(clearing));
                Assert.IsFalse(captures[i].overrideCamera);
                Assert.AreEqual(i == 5 ? 3 : 1, clearing.GetComponentsInChildren<CaptureStop>().Length);
                Assert.AreEqual(RouteLayout.Clearing(i), clearing.localPosition);
                Assert.AreEqual(Quaternion.identity, clearing.localRotation);
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
        public void TrainRailsRunNorthSouth()
        {
            var tracks = route.GetChild(2).Find("Tracks");
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = tracks.Find($"Rail {side}").GetComponent<Collider>();
                Assert.Greater(rail.bounds.size.z, rail.bounds.size.x * 10f);
                Assert.AreEqual(GroundType.Gravel, GroundSurface.TypeOf(rail));
            }
        }

        [Test]
        public void FlightRouteVisitsEveryClearingAtCruiseHeightInOrder()
        {
            Assert.Greater(RouteBuilder.SkyHeight, 7f + 0.5f);
            Assert.GreaterOrEqual(skyRoute.Length, 8);
            int cursor = 0;
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 expected = route.GetChild(i).position + Vector3.up * RouteBuilder.SkyHeight;
                int index = System.Array.FindIndex(skyRoute, cursor, point => Vector3.Distance(point, expected) < 0.001f);
                Assert.GreaterOrEqual(index, cursor, route.GetChild(i).name);
                cursor = index + 1;
            }
            for (int i = 0; i < skyRoute.Length; i++)
                Assert.IsFalse(HitsRoute(skyRoute[i], (skyRoute[(i + 1) % skyRoute.Length] - skyRoute[i]).normalized,
                    Vector3.Distance(skyRoute[i], skyRoute[(i + 1) % skyRoute.Length])), $"Vuelo {i}");
        }

        [Test]
        public void FirstFiveClearingsHaveHidesReachableFromThePathOnBakedNavMesh()
        {
            var surface = route.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
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
            Assert.Less(route.GetComponentsInChildren<Transform>(true).Length, 7000);
            var renderers = route.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            for (int i = 0; i < RouteLayout.Count; i++)
                Assert.LessOrEqual(route.GetChild(i).GetComponentsInChildren<Renderer>(true).Length, 200, route.GetChild(i).name);
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
        public void FogAndItsSixLitComparisonStopsStayOutsideTheRouteBudget()
        {
            var fog = root.GetComponentInChildren<GroundFog>();
            Assert.IsNotNull(fog);
            Assert.IsFalse(fog.transform.IsChildOf(route));
            Assert.AreSame(player.transform, fog.player);
            Assert.AreEqual(109, fog.layers.Length, "16 de claros, 54 del sendero, 8 del bosque embrujado y 31 de agua.");
            Assert.AreEqual(109, fog.GetComponentsInChildren<Renderer>().Length);
            Assert.AreEqual(31, System.Array.FindAll(fog.layers, layer => layer.water).Length);
            Assert.IsEmpty(fog.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(fog.GetComponentsInChildren<Light>());
            foreach (var layer in fog.layers)
            {
                Assert.AreSame(fog.layers[0].renderer.sharedMaterial, layer.renderer.sharedMaterial);
                Assert.AreEqual(6, layer.renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
                Assert.AreEqual(ShadowCastingMode.Off, layer.renderer.shadowCastingMode);
                Assert.IsFalse(layer.renderer.receiveShadows);
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(layer.renderer.gameObject, StaticEditorFlags.BatchingStatic));
            }
            var pathCards = new[] { 5, 19, 5, 5, 7, 8, 5, 0 };
            for (int i = 1; i <= 8; i++)
            {
                Assert.AreEqual(2, System.Array.FindAll(fog.layers,
                    layer => layer.renderer.name.StartsWith($"Clearing {i} Fog ")).Length);
                Assert.AreEqual(pathCards[i - 1], System.Array.FindAll(fog.layers,
                    layer => layer.renderer.name.StartsWith($"Segment {i} Fog ")).Length);
            }
            var stages = root.GetComponentsInChildren<FogCaptureStage>();
            Assert.AreEqual(6, stages.Length, "Cementerio, río y lago, cada uno con y sin neblina.");
            var names = new[] { "Fog 1 Cemetery", "Fog 2 River", "Fog 3 Lake" };
            var sources = new[] { route.GetChild(0).Find("Route 1 Cemetery"),
                route.GetChild(5).Find("Path to Next Clearing/River Crossing/River/Water 2 Crossing"), route.GetChild(5).Find("Water 1 Lake") };
            for (int i = 0; i < names.Length; i++)
                foreach (string suffix in new[] { " On", " Off" })
                {
                    var stage = System.Array.Find(stages, item => item.name == names[i] + suffix);
                    Assert.IsNotNull(stage);
                    Assert.AreSame(fog, stage.fog);
                    Assert.IsFalse(stage.transform.IsChildOf(route));
                    Assert.IsFalse(stage.transform.IsChildOf(fog.transform));
                    Assert.AreEqual(sources[i].position, stage.transform.position);
                    Assert.AreEqual(sources[i].rotation, stage.transform.rotation);
                    Assert.AreEqual(suffix == " On", stage.fogEnabled);
                    Assert.AreEqual(SoulEnergy.Max, stage.GetComponent<LightCaptureStage>().energy);
                    var capture = stage.GetComponent<CaptureStop>();
                    var source = sources[i].GetComponent<CaptureStop>();
                    Assert.AreEqual(source.overrideCamera, capture.overrideCamera);
                    Assert.AreEqual(source.cameraPosition, capture.cameraPosition);
                    Assert.AreEqual(source.cameraRotation, capture.cameraRotation);
                }
            var cemetery = System.Array.Find(fog.layers, layer => layer.renderer.name == "Clearing 1 Fog 1");
            var lake = System.Array.Find(fog.layers, layer => layer.renderer.name == "Clearing 6 Fog 1");
            var river = System.Array.Find(fog.layers, layer => layer.renderer.name == "Segment 6 Fog 5-1");
            var dryPath = System.Array.Find(fog.layers, layer => layer.renderer.name == "Segment 1 Fog 1-1");
            Assert.Greater(lake.density, cemetery.density);
            Assert.Greater(river.density, dryPath.density);
            var haunted = System.Array.FindAll(fog.layers, layer => layer.renderer.name.StartsWith("Haunted Forest Fog "));
            Assert.AreEqual(FogBuilder.HauntedCards, haunted.Length);
            foreach (var layer in haunted)
            {
                Assert.Greater(layer.density, cemetery.density);
                Vector3 point = route.InverseTransformPoint(layer.renderer.transform.position);
                Assert.IsTrue(new RouteTerrain().IsHauntedForest(point.x, point.z));
            }
        }

        [Test]
        public void FogFollowsEveryPathLegAndCoversTheLandscapeWaterAlongItsLength()
        {
            var fog = root.GetComponentInChildren<GroundFog>();
            for (int i = 0; i < RouteLayout.Count - 1; i++)
            {
                var points = RouteLayout.Path(i);
                for (int segment = 1; segment < points.Length; segment++)
                {
                    Vector3 start = route.TransformPoint(points[segment - 1]), corner = route.TransformPoint(points[segment]);
                    Vector3 end = route.TransformPoint(points[Mathf.Min(segment + 1, points.Length - 1)]);
                    var layers = System.Array.FindAll(fog.layers,
                        layer => layer.renderer.name.StartsWith($"Segment {i + 1} Fog {segment}-"));
                    Assert.IsNotEmpty(layers);
                    foreach (var layer in layers)
                    {
                        Assert.AreEqual(start, layer.pathStart);
                        Assert.AreEqual(corner, layer.pathCorner);
                        Assert.AreEqual(end, layer.pathEnd);
                        Assert.Less(FogDensity.DistanceToSegment(layer.renderer.transform.position, start, corner), 0.001f);
                        Assert.Greater(Vector3.Dot(layer.renderer.transform.up,
                            Vector3.ProjectOnPlane(corner - start, Vector3.up).normalized), 0.999f);
                    }
                    for (int sample = 0; sample <= 10; sample++)
                        Assert.IsTrue(System.Array.Exists(layers, layer => Covers(layer, Vector3.Lerp(start, corner, sample / 10f))));
                }
            }
            int surfaces = 0;
            foreach (var water in route.GetComponentsInChildren<Renderer>())
            {
                if (water.sharedMaterial.shader.name != "Universal Render Pipeline/Tiffany Water") continue;
                surfaces++;
                var layers = System.Array.FindAll(fog.layers, layer => layer.water
                    && Vector2.Distance(new Vector2(layer.waterFootprint.x, layer.waterFootprint.y),
                        new Vector2(water.bounds.center.x, water.bounds.center.z)) < 0.001f);
                Assert.IsNotEmpty(layers, water.name);
                if (water.bounds.size.magnitude > FogBuilder.WaterCardSize) Assert.Greater(layers.Length, 1, water.name);
                var bounds = water.localBounds;
                for (int x = 0; x <= 10; x++)
                for (int z = 0; z <= 10; z++)
                {
                    Vector3 point = water.transform.TransformPoint(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 10f),
                        bounds.center.y, Mathf.Lerp(bounds.min.z, bounds.max.z, z / 10f)));
                    Assert.IsTrue(System.Array.Exists(layers, layer => Covers(layer, point)), $"Agua {water.name}: {point}");
                }
            }
            Assert.AreEqual(7, surfaces, "Lago, cruce y cinco tramos del río en Landscape.");
            bool Covers(GroundFog.Layer layer, Vector3 point)
            {
                point.y = layer.renderer.transform.position.y;
                Vector3 local = layer.renderer.transform.InverseTransformPoint(point);
                return Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.y) < 0.5f;
            }
        }

        [Test]
        public void PathsJoinIrregularClearingsWithOpenSidesIntoTheForest()
        {
            var terrain = new RouteTerrain();
            Assert.IsNull(route.Find("Landscape/Walkable Boundary"));
            var borders = route.Find("Landscape/Map Boundary").GetComponentsInChildren<Collider>();
            Assert.IsNotEmpty(borders);
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var clearing = route.GetChild(i);
                Vector3 center = RouteLayout.Clearing(i);
                for (int degrees = 0; degrees < 360; degrees += 10)
                {
                    float angle = degrees * Mathf.Deg2Rad;
                    var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    float radius = terrain.ClearingRadius(i, angle);
                    Vector3 beyond = center + normal * (radius + 1f);
                    if (terrain.IsWalkable(beyond.x, beyond.z)) continue;
                    var ray = new Ray(clearing.TransformPoint(normal * (radius - 1f) + Vector3.up), normal);
                    Assert.IsFalse(System.Array.Exists(borders, border => border.Raycast(ray, out _, 2.2f)), $"Borde {i}/{degrees}");
                }
                foreach (Vector3 opening in RouteLayout.Openings(i))
                {
                    Vector3 local = opening - center;
                    Vector3 normal = Mathf.Abs(local.x) > Mathf.Abs(local.z)
                        ? Vector3.right * Mathf.Sign(local.x) : Vector3.forward * Mathf.Sign(local.z);
                    var ray = new Ray(route.TransformPoint(opening - normal * 2f + Vector3.up), normal);
                    Assert.IsFalse(System.Array.Exists(borders, border => border.Raycast(ray, out _, 4f)), $"Entrada {i}");
                }
                foreach (Transform child in clearing)
                    if (child.TryGetComponent<GroundSurface>(out var surface))
                    {
                        var bounds = surface.GetComponent<Collider>().bounds;
                        Assert.LessOrEqual(bounds.max.x - clearing.position.x, 18f + 0.001f);
                        Assert.GreaterOrEqual(bounds.min.x - clearing.position.x, -18f - 0.001f);
                        Assert.LessOrEqual(bounds.max.z - clearing.position.z, 18f + 0.001f);
                        Assert.GreaterOrEqual(bounds.min.z - clearing.position.z, -18f - 0.001f);
                    }
                Assert.AreEqual(i < RouteLayout.Count - 1, clearing.Find("Path to Next Clearing") != null);
                Assert.IsFalse(clearing.Find("Ground").GetComponent<Renderer>().enabled);
            }
        }

        [Test]
        public void DirtPathFloorsCoverEverySegmentAndTheirJointsHaveNoStepAboveFifteenCentimeters()
        {
            RouteLayout.RiverSpan(out var riverStart, out var riverEnd);
            for (int from = 0; from < RouteLayout.Count - 1; from++)
            {
                var path = RouteLayout.Path(from);
                var pathRoot = route.GetChild(from).Find("Path to Next Clearing");
                var floors = pathRoot.GetComponentsInChildren<GroundSurface>();
                for (int i = 1; i < path.Length; i++)
                {
                    Vector3 a = path[i - 1], b = path[i];
                    bool river = a == riverStart && b == riverEnd;
                    Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized;
                    int samples = Mathf.CeilToInt(Vector3.Distance(a, b) * 4f);
                    for (int sample = 0; sample <= samples; sample++)
                    for (int lane = -1; lane <= 1; lane++)
                    {
                        if (river) continue;
                        Vector3 point = route.TransformPoint(Vector3.Lerp(a, b, (float)sample / samples) + side * (lane * 2f));
                        Assert.IsTrue(FloorHeights(floors, point, out _, out _), $"Hueco en sendero {from}, tramo {i}, {point}");
                    }
                    if (i == path.Length - 1) continue;
                    for (float x = -3f; x <= 3f; x += 0.25f)
                    for (float z = -3f; z <= 3f; z += 0.25f)
                    {
                        Vector3 point = route.TransformPoint(b + new Vector3(x, 0f, z));
                        if (FloorHeights(floors, point, out float low, out float high))
                            Assert.LessOrEqual(high - low, 0.15f + 0.001f, $"Unión {from}/{i}, {point}");
                    }
                }
            }
        }

        static bool FloorHeights(GroundSurface[] floors, Vector3 point, out float low, out float high)
        {
            low = float.PositiveInfinity;
            high = float.NegativeInfinity;
            foreach (var floor in floors)
                if (floor.GetComponent<Collider>().Raycast(new Ray(point + Vector3.up * 2f, Vector3.down), out var hit, 4f)
                    && hit.normal.y >= Mathf.Cos(RouteLayout.CliffPathMaxSlopeDegrees * Mathf.Deg2Rad) - 0.001f)
                {
                    low = Mathf.Min(low, hit.point.y);
                    high = Mathf.Max(high, hit.point.y);
                }
            return low <= high;
        }

        [Test]
        public void GateAndCliffAreSolidAndTheBakedRouteConnectsEveryClearing()
        {
            var gate = route.Find("Landscape/Fence/Closed Gate");
            Assert.IsNotNull(gate.GetComponent<Collider>());
            Assert.IsFalse(gate.GetComponent<Collider>().isTrigger);
            RouteLayout.Gate(out var position, out _, out float width);
            Assert.Less(Vector3.Distance(route.TransformPoint(position), gate.position), 0.001f);
            Assert.AreEqual(width, gate.GetComponent<BoxCollider>().size.z, 0.001f);
            Assert.IsNull(route.GetChild(5).Find("Cliff"));
            Assert.IsNull(route.GetChild(7).Find("Gate"));
            var terrainColliders = route.Find("Landscape/Terrain").GetComponentsInChildren<MeshCollider>();
            float SurfaceHeight(Vector2 point)
            {
                var ray = new Ray(route.TransformPoint(new Vector3(point.x, 20f, point.y)), Vector3.down);
                foreach (var collider in terrainColliders)
                    if (collider.Raycast(ray, out var hit, 40f)) return route.InverseTransformPoint(hit.point).y;
                Assert.Fail("Falta terreno bajo el acantilado.");
                return 0f;
            }
            Assert.GreaterOrEqual(SurfaceHeight(RouteTerrain.Plateau) - RouteLayout.Clearing(5).y, 5.99f);
            Assert.Less(SurfaceHeight(RouteTerrain.CliffFoot), 1.5f);
            var surface = route.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.layerMask = ~LayerMask.GetMask(ForestLayout.CoverLayer, "Ignore Raycast");
            surface.BuildNavMesh();
            navMeshData = surface.navMeshData;
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out var start, 0.5f, NavMesh.AllAreas));
            Vector3 last = start.position;
            for (int i = 1; i < RouteLayout.Count; i++)
            {
                Assert.IsTrue(NavMesh.SamplePosition(route.GetChild(i).TransformPoint(Vector3.back * 13f), out var end, 0.5f, NavMesh.AllAreas));
                Assert.IsTrue(NavMesh.CalculatePath(last, end.position, NavMesh.AllAreas, path), $"Claro {i}");
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Claro {i}");
                last = end.position;
            }
            Assert.IsFalse(NavMesh.SamplePosition(gate.position, out _, 0.1f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(gate.position + Vector3.left * 2f, out var gateWest, 0.5f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(gate.position + Vector3.right * 2f, out var gateEast, 0.5f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.Raycast(gateWest.position, gateEast.position, out _, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.CalculatePath(last, start.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 delta = corners[i] - corners[i - 1];
                length += delta.magnitude;
                Assert.IsFalse(gate.GetComponent<Collider>().Raycast(new Ray(corners[i - 1] + Vector3.up * 0.6f, delta.normalized),
                    out _, delta.magnitude));
            }
            Assert.Greater(length, Vector3.Distance(last, start.position) * 2f);
            var terrain = new RouteTerrain();
            int forest = NavMesh.GetAreaFromName(RouteTerrain.ForestArea);
            Assert.Greater(forest, 0);
            Assert.AreEqual(4f, NavMesh.GetAreaCost(forest));
            Assert.AreEqual(4f, PlaythroughNavigation.CreateFilter().GetAreaCost(forest));
            Assert.IsTrue(NavMesh.SamplePosition(route.TransformPoint(new Vector3(198f, terrain.GroundHeight(198f, 15f), 15f)),
                out var woods, 3f, 1 << forest));
            Assert.IsTrue(NavMesh.CalculatePath(start.position, woods.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            var tracks = RouteLayout.Clearing(2);
            int walkable = 1 << NavMesh.GetAreaFromName("Walkable");
            Vector3 previous = Vector3.zero;
            for (float z = tracks.z; z <= RouteLayout.TrainNorthEnd; z += 2f)
            {
                Vector3 point = route.TransformPoint(new Vector3(tracks.x, terrain.TrainHeight(z), z));
                Assert.IsTrue(NavMesh.SamplePosition(point, out var deck, 0.5f, walkable), $"Puente en {z}");
                if (z > tracks.z)
                {
                    Assert.IsTrue(NavMesh.CalculatePath(previous, deck.position, walkable, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Puente en {z}");
                }
                previous = deck.position;
            }
            var navigation = new PlaythroughNavigation();
            for (int i = 0; i < RouteLayout.Count - 1; i++)
            {
                Vector3 origin = route.GetChild(i).TransformPoint(Vector3.back * 13f);
                Vector3 target = route.GetChild(i + 1).TransformPoint(Vector3.back * 13f);
                Assert.IsTrue(NavMesh.SamplePosition(origin, out var from, 0.5f, walkable));
                Assert.IsTrue(NavMesh.SamplePosition(target, out var to, 0.5f, walkable));
                Assert.IsTrue(NavMesh.CalculatePath(from.position, to.position, walkable, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Senderos {i}");
                Assert.IsTrue(navigation.Calculate(origin, target), $"Partida automática {i}");
            }
        }

        [Test]
        public void LakeHasNoWalkableWaterAndItsBanksBlockEntry()
        {
            var lake = route.GetChild(5);
            var water = lake.Find("Water");
            Assert.IsNull(water.GetComponent<Collider>());
            Assert.Greater(water.GetComponent<Renderer>().bounds.size.x, 100f);
            Assert.Greater(water.GetComponent<Renderer>().bounds.size.z, 90f);
            Assert.IsTrue(HitsRoute(water.position + Vector3.up * 2f, Vector3.down, 7f));
            var terrain = new RouteTerrain();
            var mesh = water.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(AssetDatabase.Contains(mesh));
            var vertices = mesh.vertices;
            for (int i = 1; i < vertices.Length; i++)
            {
                Vector3 point = route.InverseTransformPoint(water.TransformPoint(vertices[i]));
                Vector2 shore = RouteTerrain.LakeShorePoint((i - 1) * Mathf.PI * 2f / (vertices.Length - 1));
                Assert.Less(Vector2.Distance(new Vector2(point.x, point.z), shore), 0.001f);
                Assert.AreEqual(-0.15f, point.y, 0.001f);
            }
            var shores = route.Find("Landscape/Lake Banks").GetComponentsInChildren<Collider>();
            for (int degrees = 0; degrees < 360; degrees += 15)
            {
                float angle = degrees * Mathf.Deg2Rad;
                var point = RouteTerrain.LakeShorePoint(angle);
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var ray = new Ray(route.TransformPoint(new Vector3(point.x, terrain.GroundHeight(point.x, point.y) + 1f, point.y)
                    + normal * 2f), -normal);
                Assert.IsTrue(System.Array.Exists(shores, shore => shore.Raycast(ray, out _, 4f)));
            }
        }

        [Test]
        public void RiverHasFiveGravelStonesOneBranchLogAndBlocksWaterBesideTheCrossing()
        {
            var river = route.GetChild(5).Find("Path to Next Clearing/River Crossing/River");
            Assert.IsNotNull(river);
            Assert.IsNull(river.Find("Water").GetComponent<Collider>());
            Assert.IsNull(river.parent.Find("Ground"));
            RouteLayout.RiverSpan(out var start, out var end);
            Assert.Less(Vector3.Distance(route.TransformPoint((start + end) * 0.5f), river.position), 0.001f);
            Assert.Greater(Vector3.Dot(river.forward, (end - start).normalized), 0.999f);
            foreach (var surface in river.parent.parent.GetComponentsInChildren<GroundSurface>())
                if (surface.type == GroundType.Dirt)
                    for (int z = -1; z <= 1; z++)
                        Assert.IsFalse(surface.GetComponent<Collider>().Raycast(
                            new Ray(river.TransformPoint(new Vector3(0f, 2f, z)), Vector3.down), out _, 4f), surface.name);
            var surfaces = river.GetComponentsInChildren<GroundSurface>();
            Assert.AreEqual(6, surfaces.Length);
            for (int i = 1; i <= 5; i++)
            {
                var stone = river.Find($"Stone {i}").GetComponent<BoxCollider>();
                Assert.AreEqual(GroundType.Gravel, GroundSurface.TypeOf(stone));
                Assert.AreEqual(river.position.y + WaterBuilder.StoneTop, stone.bounds.max.y, 0.001f);
                Assert.Greater(stone.size.z, CrossingLayout.MaxWalkingSpacing);
            }
            Assert.AreEqual(GroundType.Branches, river.Find("Log").GetComponent<GroundSurface>().type);
            for (int side = -1; side <= 1; side += 2)
            {
                Assert.IsTrue(HitsRoute(river.TransformPoint(new Vector3(side * 1.8f, 0.6f, -3f)), Vector3.forward, 2f));
                Assert.IsTrue(HitsRoute(river.TransformPoint(new Vector3(side * 1.8f, 2f, 0f)), Vector3.down, 4f));
            }
            Assert.IsFalse(HitsRoute(river.TransformPoint(new Vector3(0f, 0.6f, -3f)), Vector3.forward, 6f));
        }

        [Test]
        public void WaterHasSavedShaderPositionedLoopsAndLitCaptureStops()
        {
            var lake = route.GetChild(5).Find("Water");
            var river = route.GetChild(5).Find("Path to Next Clearing/River Crossing/River/Water");
            foreach (var water in new[] { lake, river })
            {
                var material = water.GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(AssetDatabase.Contains(material));
                Assert.AreEqual("Universal Render Pipeline/Tiffany Water", material.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                Assert.Less(material.GetColor("_BaseColor").a, 1f);
                var source = water.GetComponent<AudioSource>();
                Assert.IsNotNull(source.clip);
                Assert.AreEqual(water == lake ? "lake_loop" : "river_loop", source.clip.name);
                Assert.IsTrue(source.loop);
                Assert.IsTrue(source.playOnAwake);
                Assert.AreEqual(1f, source.spatialBlend);
                Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
                Assert.AreEqual(36f, source.minDistance, "La cámara y su AudioListener van a unos 36 m de Tiffany.");
                Assert.AreEqual(water == lake ? 120f : 60f, source.maxDistance);
            }
            var lakeStop = route.GetChild(5).Find("Water 1 Lake");
            Assert.Less(Vector3.Distance(lakeStop.GetComponent<CaptureStop>().cameraPosition, lake.position), 80f);
            var riverStop = river.parent.Find("Water 2 Crossing");
            foreach (var stop in new[] { lakeStop, riverStop })
            {
                Assert.IsNotNull(stop.GetComponent<LightCaptureStage>());
                Assert.IsTrue(stop.GetComponent<CaptureStop>().overrideCamera);
            }
            Assert.IsTrue(HitsRoute(riverStop.position + Vector3.up, Vector3.down, 1.1f));
            Assert.AreEqual(WaterBuilder.StoneTop, riverStop.localPosition.y, 0.001f);
        }

        [Test]
        public void ObstaclesStayOffNavMeshAndElevatedGroundSurfacesRemainWalkable()
        {
            int roofCount = 0;
            int tankCount = 0;
            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            Assert.GreaterOrEqual(notWalkable, 0);
            foreach (Transform clearing in route)
            foreach (var collider in clearing.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.TryGetComponent<GroundSurface>(out _)) continue;
                if (collider.bounds.max.y <= clearing.position.y + 1f && collider.bounds.size.y <= 1f) continue;
                var modifier = collider.GetComponent<NavMeshModifier>();
                Assert.IsNotNull(modifier, collider.name);
                Assert.IsTrue(modifier.isActiveAndEnabled, collider.name);
                if (collider.gameObject.layer == LayerMask.NameToLayer(ForestLayout.CoverLayer))
                {
                    Assert.IsTrue(modifier.ignoreFromBuild, collider.name);
                    Vector3 point = route.InverseTransformPoint(collider.bounds.center);
                    Assert.Greater(collider.bounds.min.y - route.position.y - new RouteTerrain().GroundHeight(point.x, point.z), 2.5f);
                    continue;
                }
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
            {
                var modifier = ground.GetComponentInParent<NavMeshModifier>();
                if (ground.name.EndsWith(" Wild")) Assert.AreEqual(NavMesh.GetAreaFromName(RouteTerrain.ForestArea), modifier.area);
                else if (modifier != null) Assert.IsTrue(modifier.ignoreFromBuild, ground.name);
            }
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
                        bool blocked = renderer.TryGetComponent<MeshCollider>(out var mesh)
                            ? mesh.Raycast(ray, out _, direction.magnitude)
                            : renderer.bounds.IntersectRay(ray, out float distance) && distance < direction.magnitude;
                        Assert.IsFalse(blocked,
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
