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
        string previousModelMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            previousMaterialFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = TempFolder + "/Materials";
            previousModelMaterialFolder = ModelLibrary.materialFolder;
            ModelLibrary.materialFolder = TempFolder + "/ModelMaterials";
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
            ModelLibrary.materialFolder = previousModelMaterialFolder;
        }

        [Test]
        public void EchoTylerMaterialsStayInsideTheTestFolder()
        {
            var echoes = root.GetComponentsInChildren<EchoPlayer>(true);
            Assert.AreEqual(5, echoes.Length);
            foreach (var echo in echoes)
            {
                var renderers = echo.tyler.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers);
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        Assert.AreEqual($"{TempFolder}/ModelMaterials/{material.name}.mat", AssetDatabase.GetAssetPath(material));
            }
        }

        [Test]
        public void BuildingsAndForestUseOcclusionWhileGroundKeepsItsShader()
        {
            foreach (var clearing in new[] { route.GetChild(3), route.GetChild(4), route.GetChild(7) })
            {
                string building = clearing == route.GetChild(3) ? "Barn" : clearing == route.GetChild(4) ? "Hut" : "House";
                foreach (var renderer in clearing.Find(building).GetComponentsInChildren<Renderer>(true))
                    Assert.AreEqual(GeneratedMaterials.OcclusionShader, renderer.sharedMaterial.shader.name, renderer.name);
                foreach (var renderer in clearing.Find("Forest").GetComponentsInChildren<Renderer>(true))
                    Assert.AreEqual(GeneratedMaterials.OcclusionShader, renderer.sharedMaterial.shader.name, renderer.name);
                Assert.AreEqual("Universal Render Pipeline/Tiffany Forest Ground",
                    clearing.Find("Ground").GetComponent<Renderer>().sharedMaterial.shader.name);
            }
            Assert.AreEqual("Universal Render Pipeline/Lit", route.GetChild(0).Find("Ball").GetComponent<Renderer>().sharedMaterial.shader.name);
            foreach (var movable in route.GetComponentsInChildren<MovableObject>(true))
                Assert.AreNotEqual(GeneratedMaterials.OcclusionShader, movable.GetComponent<Renderer>().sharedMaterial.shader.name);
            foreach (var candle in route.GetComponentsInChildren<CandleSpot>(true))
                foreach (var renderer in candle.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Assert.AreNotEqual(GeneratedMaterials.OcclusionShader, material.shader.name, renderer.name);
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
            Assert.AreEqual(18, captures.Length, "Ocho claros, dos vistas del agua y dos vistas por cada uno de los cuatro retos.");
            captures = System.Array.FindAll(captures, capture => capture.name.StartsWith("Route "));
            System.Array.Sort(captures, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < names.Length; i++)
            {
                var clearing = route.GetChild(i);
                Assert.AreEqual(names[i], clearing.name);
                Assert.AreEqual(names[i], captures[i].name);
                Assert.IsTrue(captures[i].transform.IsChildOf(clearing));
                Assert.IsFalse(captures[i].overrideCamera);
                int challenges = i == 3 ? 2 : i == 1 || i == 2 ? 1 : 0;
                Assert.AreEqual((i == 5 ? 3 : 1) + challenges * 2, clearing.GetComponentsInChildren<CaptureStop>().Length);
                Assert.AreEqual(RouteLayout.Clearing(i), clearing.localPosition);
                Assert.AreEqual(Quaternion.identity, clearing.localRotation);
                var ground = clearing.GetComponentInChildren<GroundSurface>();
                Assert.AreEqual(groundTypes[i], ground.type);
                float size = RouteLayout.ClearingSizeOf(i);
                Assert.AreEqual(size, ground.GetComponent<Collider>().bounds.size.x);
                Assert.AreEqual(size, ground.GetComponent<Collider>().bounds.size.z);
                Assert.GreaterOrEqual(clearing.position.x - size * 0.5f, 90f);
                var candles = clearing.GetComponentsInChildren<CandleSpot>();
                Assert.AreEqual(1 + challenges + (i == 1 || i == 3 ? 2 : 0), candles.Length);
                Assert.AreEqual(CandleKind.Family, candles[0].kind);
                Assert.AreSame(player.GetComponent<PlayerSoul>(), candles[0].player);
                Assert.AreSame(storm, candles[0].storm);
                Assert.IsNotNull(candles[0].flame);
                Assert.IsTrue(candles[0].flame.transform.IsChildOf(candles[0].transform));
            }
            Assert.IsNotNull(route.GetChild(7).Find("Window Candle"));
        }

        [Test]
        public void ClearingsAndPathsKeepSurfaceTypesWithTexturedMeterScaleGround()
        {
            var types = new[] { GroundType.Grass, GroundType.Slab, GroundType.Gravel, GroundType.Branches,
                GroundType.Leaves, GroundType.Grass, GroundType.Slab, GroundType.Dirt };
            CollectionAssert.AreEqual(types, RouteBuilder.ClearingGrounds);
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var clearing = route.GetChild(i);
                AssertGround(clearing.Find("Ground"), types[i]);
                if (i == 7) continue;
                var path = clearing.Find("Path to Next Clearing");
                int floors = 0;
                foreach (Transform piece in path)
                {
                    if (!piece.name.StartsWith("Ground ")) continue;
                    AssertGround(piece, GroundType.Dirt);
                    floors++;
                }
                Assert.Greater(floors, 0, clearing.name);
                if (i == 5)
                    foreach (string name in new[] { "South Bank", "North Bank" })
                        AssertGround(path.Find("River Crossing/" + name), GroundType.Dirt);
            }
        }

        [Test]
        public void EveryGroundSurfaceUsesTheMaterialForItsFootstepType()
        {
            var surfaces = route.GetComponentsInChildren<GroundSurface>(true);
            Assert.IsNotEmpty(surfaces);
            foreach (var surface in surfaces)
            {
                var expected = GroundMaterials.For(surface.type);
                var renderers = surface.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, surface.name);
                foreach (var renderer in renderers)
                {
                    if (renderer.TryGetComponent<MeshFilter>(out var filter)
                        && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(ModelLibrary.ModelFolder + "/river-")) continue;
                    Assert.IsNotEmpty(renderer.sharedMaterials, surface.name);
                    foreach (var material in renderer.sharedMaterials)
                        Assert.AreSame(expected, material, $"{surface.name} ({surface.type}), renderer {renderer.name}");
                }
            }
        }

        static void AssertGround(Transform ground, GroundType type)
        {
            Assert.IsNotNull(ground);
            Assert.AreEqual(type, GroundSurface.TypeOf(ground.GetComponent<Collider>()));
            Assert.AreEqual(type, ground.GetComponent<GroundSurface>().type);
            var material = ground.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("Route" + type, material.name);
            Assert.AreNotEqual("RouteGround", material.name);
            string textureName = type switch
            {
                GroundType.Dirt or GroundType.Grass => "dry_ground_rocks",
                GroundType.Slab => "rock_boulder_dry",
                GroundType.Gravel => "gravel_ground_01",
                GroundType.Leaves => "dry_decay_leaves",
                GroundType.Branches => "burned_ground_01",
                _ => throw new System.ArgumentOutOfRangeException(nameof(type))
            };
            Assert.AreEqual(textureName + "_diff", material.GetTexture("_BaseMap").name);
            Assert.AreEqual(textureName + "_nor_gl", material.GetTexture("_BumpMap").name);
            Assert.IsTrue(AssetDatabase.Contains(material));
            Assert.AreEqual("Universal Render Pipeline/Tiffany Forest Ground", material.shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Assert.AreEqual(Vector2.one / GroundMaterials.TileMeters, material.GetTextureScale("_BaseMap"));
            Assert.Greater(material.GetFloat("_Mix"), 0f);
            Assert.AreEqual(0f, material.GetFloat("_Patch"));
            Assert.AreEqual(1f, material.GetFloat("_ZWrite"));
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_DetailMap", "_DetailNormal" })
            {
                var texture = material.GetTexture(property);
                Assert.IsNotNull(texture, ground.name + " " + property);
                Assert.IsTrue(AssetDatabase.Contains(texture));
                Assert.LessOrEqual(Mathf.Max(texture.width, texture.height), 1024);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode);
                Assert.IsTrue(importer.mipmapEnabled);
                Assert.AreNotEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
                if (property == "_BumpMap" || property == "_DetailNormal") Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType);
            }
        }

        [Test]
        public void ClearingsFitGeometryBudgetIncludingInstancedFloorCover()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var clearing = route.GetChild(i);
                var renderers = clearing.GetComponentsInChildren<Renderer>(true);
                Assert.LessOrEqual(renderers.Length, 200, clearing.name);
                long triangles = 0;
                int draws = 0;
                foreach (var renderer in renderers)
                {
                    draws += renderer.sharedMaterials.Length;
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) triangles += mesh.GetIndexCount(submesh) / 3;
                }
                foreach (var instances in clearing.GetComponentsInChildren<ForestInstances>())
                foreach (var batch in instances.data.batches)
                {
                    triangles += batch.mesh.GetIndexCount(0) / 3 * batch.matrices.Length;
                    draws += Mathf.CeilToInt(batch.matrices.Length / (float)ForestInstances.MaxInstancesPerDraw);
                }
                Assert.LessOrEqual(draws, 150, clearing.name);
                Assert.LessOrEqual(triangles, 150000, clearing.name);
            }
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
        public void FourChallengesFollowTheirClearingSegmentsAndStayWithinRendererBudget()
        {
            var tracker = route.GetComponent<WitchChapterTracker>();
            var challenges = route.GetComponentsInChildren<PowerChallenge>();
            Assert.AreEqual(4, challenges.Length);
            int[] sources = { 1, 2, 3, 3 };
            int[] segments = { 4, 2, 2, 5 };
            float[] fractions = { 0.5f, 0.5f, 0.15f, 0.35f };
            var terrain = new RouteTerrain();
            foreach (var challenge in challenges)
            {
                int i = (int)challenge.kind;
                var source = tracker.clearings[sources[i]];
                var points = RouteLayout.Path(sources[i]);
                Vector3 start = points[segments[i] - 1], end = points[segments[i]];
                Assert.AreSame(tracker, challenge.tracker);
                Assert.AreSame(source, challenge.transform.parent);
                Vector3 expected = Vector3.Lerp(start, end, fractions[i]);
                expected.y = terrain.Height(expected.x, expected.z) + TerrainBuilder.FloorOffset;
                Assert.Less(Vector3.Distance(route.TransformPoint(expected), challenge.transform.position), 0.001f);
                Assert.Greater(Vector3.Dot(challenge.transform.forward,
                    route.TransformDirection(Vector3.ProjectOnPlane(end - start, Vector3.up)).normalized), 0.999f);
                Assert.AreEqual(CandleKind.Family, challenge.checkpoint.kind);
                Assert.Less(challenge.checkpoint.transform.localPosition.z, -3f);
                Assert.AreEqual(i == 0 || i == 3 ? 2 : 0, challenge.candles.Length);
                Assert.AreEqual(i == 1 || i == 3, challenge.lure != null);
                foreach (Vector3 point in new[] { challenge.Entry, challenge.Exit, challenge.RechargePoint, challenge.post.position })
                {
                    Vector3 local = route.InverseTransformPoint(point);
                    Assert.AreEqual(terrain.Height(local.x, local.z) + TerrainBuilder.FloorOffset, local.y, 0.15f);
                }
                if (challenge.lure != null)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(challenge.lure.transform.position - challenge.Entry, Vector3.up).normalized;
                    Vector3 origin = challenge.Entry + Vector3.up * 3.2f - direction * 0.4f;
                    Vector3 ray = challenge.lure.transform.position - origin;
                    Assert.IsTrue(!Physics.Raycast(origin, ray, out var hit, ray.magnitude, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<MovableObject>() == challenge.lure,
                        "La campana debe verse desde la entrada para poder moverla con la luz.");
                }
                var captures = challenge.GetComponentsInChildren<ChallengeCaptureStage>();
                Assert.AreEqual(2, captures.Length);
                CollectionAssert.AreEquivalent(new[] { challenge.name + " Before", challenge.name + " After" },
                    System.Array.ConvertAll(captures, stage => stage.name));
            }
            var stable = System.Array.Find(challenges, challenge => challenge.kind == ChallengeKind.Vanish);
            var hut = System.Array.Find(challenges, challenge => challenge.kind == ChallengeKind.Hut);
            float rechargeClearance = Vector3.Dot(hut.checkpoint.transform.position - stable.Exit, stable.transform.forward);
            Assert.Greater(rechargeClearance, hut.checkpoint.radius + NavMesh.GetSettingsByIndex(0).agentRadius,
                "La zona segura de la choza debe dejar libre la salida del pasillo del establo.");
            foreach (var clearing in tracker.clearings)
                Assert.LessOrEqual(clearing.GetComponentsInChildren<Renderer>(true).Length, 200, clearing.name);
        }

        [Test]
        public void ChallengeInteractionPointsAndCapturesStayAboveTheirSupportingGround()
        {
            var terrain = new RouteTerrain();
            var floors = route.GetComponentsInChildren<GroundSurface>();
            foreach (var challenge in route.GetComponentsInChildren<PowerChallenge>())
            {
                Check(challenge.Entry, challenge.name);
                Check(challenge.Exit, challenge.name);
                Check(challenge.post.position, challenge.name);
                foreach (var candle in challenge.GetComponentsInChildren<CandleSpot>()) Check(candle.transform.position, candle.name);
                if (challenge.lure != null)
                {
                    var bounds = challenge.lure.GetComponent<Collider>().bounds;
                    Check(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), challenge.lure.name);
                }
                foreach (var stage in challenge.GetComponentsInChildren<ChallengeCaptureStage>())
                {
                    Check(stage.transform.position, stage.name);
                    Assert.DoesNotThrow(stage.FrameCamera, stage.name);
                }
            }

            void Check(Vector3 position, string name)
            {
                Vector3 local = route.InverseTransformPoint(position);
                Assert.AreEqual(terrain.Height(local.x, local.z) + TerrainBuilder.FloorOffset, local.y, 0.15f, name);
                bool supported = false;
                foreach (var floor in floors)
                {
                    if (!floor.GetComponent<Collider>().Raycast(new Ray(position + Vector3.up * 2f, Vector3.down), out var hit, 4f)) continue;
                    Assert.GreaterOrEqual(position.y + 0.005f, hit.point.y, name + ": " + floor.name);
                    supported |= position.y - hit.point.y < 0.15f;
                }
                Assert.IsTrue(supported, name);
            }
        }

        [Test]
        public void ChallengeCameraStillRejectsAnOccludedCandle()
        {
            var challenge = System.Array.Find(route.GetComponentsInChildren<PowerChallenge>(), item => item.kind == ChallengeKind.Vanish);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Challenge Occluder";
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.position = challenge.checkpoint.transform.position + Vector3.up;
            blocker.transform.localScale = new Vector3(3f, 5f, 3f);
            var stage = challenge.GetComponentInChildren<ChallengeCaptureStage>();
            var error = Assert.Throws<System.InvalidOperationException>(stage.FrameCamera);
            StringAssert.Contains("Challenge Occluder", error.Message);
        }

        [Test]
        public void ForestLeavesChallengePassagesAndLureAlcovesClear()
        {
            foreach (var challenge in route.GetComponentsInChildren<PowerChallenge>())
            {
                foreach (Transform forest in new[] { route.Find("Landscape/Forest Fill"), challenge.transform.parent.Find("Forest") })
                foreach (var trunk in forest.GetComponentsInChildren<CapsuleCollider>())
                {
                    Vector3 point = route.InverseTransformPoint(trunk.transform.TransformPoint(trunk.center));
                    Assert.IsFalse(ChallengeBuilder.Reserves(point, trunk.radius * trunk.transform.lossyScale.x), trunk.name);
                }
            }
        }

        [Test]
        public void ConsecutiveClearingsLeaveATrailBetweenThem()
        {
            for (int i = 1; i < RouteLayout.Count; i++)
            {
                Vector3 gap = route.GetChild(i).position - route.GetChild(i - 1).position;
                gap.y = 0f;
                Assert.Greater(gap.magnitude, 2f * RouteBuilder.TrailRadius + 4f, route.GetChild(i).name);
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
                Assert.IsTrue(NavMesh.SamplePosition(ClearingEntry(i),
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
                Assert.AreEqual(!ChallengeBuilder.IsMoving(renderer.transform),
                    GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic), renderer.name);
                if (renderer.GetComponentInParent<PowerChallenge>() == null)
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
            Assert.AreEqual(17, lights.Length, "Ocho veladoras originales, la de la rampa, cuatro de recarga y cuatro para encender.");
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
            Assert.AreEqual(100, fog.layers.Length, "16 de claros, 54 del sendero, 8 del bosque embrujado y 22 de agua.");
            Assert.AreEqual(100, fog.GetComponentsInChildren<Renderer>().Length);
            Assert.AreEqual(22, System.Array.FindAll(fog.layers, layer => layer.water).Length);
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
                var cards = System.Array.FindAll(fog.layers, layer => layer.renderer.name.StartsWith($"Clearing {i} Fog "));
                Assert.AreEqual(2, cards.Length);
                foreach (var card in cards)
                {
                    float scale = RouteLayout.ClearingSizeOf(i - 1) / RouteLayout.ClearingSize;
                    Assert.AreEqual(new Vector3(24f * scale, 40f * scale, 1f), card.renderer.transform.localScale);
                    Assert.AreEqual(10f * scale, Vector3.ProjectOnPlane(card.renderer.transform.position
                        - route.GetChild(i - 1).position, Vector3.up).magnitude, 0.001f);
                }
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
                Vector3 size = Vector3.Scale(water.localBounds.size, water.transform.lossyScale);
                if (Mathf.Max(size.x, size.z) > FogBuilder.WaterCardSize) Assert.Greater(layers.Length, 1, water.name);
                var bounds = water.localBounds;
                for (int x = 0; x <= 10; x++)
                for (int z = 0; z <= 10; z++)
                {
                    Vector3 point = water.transform.TransformPoint(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 10f),
                        bounds.center.y, Mathf.Lerp(bounds.min.z, bounds.max.z, z / 10f)));
                    Assert.IsTrue(System.Array.Exists(layers, layer => Covers(layer, point)), $"Agua {water.name}: {point}");
                }
            }
            Assert.AreEqual(6, surfaces, "Lago y cinco tramos del río en Landscape.");
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
                        float half = RouteLayout.ClearingSizeOf(i) * 0.5f;
                        Assert.LessOrEqual(bounds.max.x - clearing.position.x, half + 0.001f);
                        Assert.GreaterOrEqual(bounds.min.x - clearing.position.x, -half - 0.001f);
                        Assert.LessOrEqual(bounds.max.z - clearing.position.z, half + 0.001f);
                        Assert.GreaterOrEqual(bounds.min.z - clearing.position.z, -half - 0.001f);
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
            Assert.IsNotNull(gate, "Falta Landscape/Fence/Closed Gate.");
            Assert.IsNotNull(gate.GetComponent<BoxCollider>(), $"La puerta en {gate.position:F3} debe tener colisión sólida.");
            Assert.IsFalse(gate.GetComponent<Collider>().isTrigger, $"La puerta en {gate.position:F3} no puede ser un trigger.");
            RouteLayout.Gate(out var position, out _, out float width);
            Assert.Less(Vector3.Distance(route.TransformPoint(position), gate.position), 0.001f,
                $"Puerta fuera de sitio: {gate.position:F3}, esperado {route.TransformPoint(position):F3}.");
            Assert.AreEqual(width, gate.GetComponent<BoxCollider>().size.z, 0.001f, $"Ancho de la puerta en {gate.position:F3}.");
            Assert.IsNull(route.GetChild(5).Find("Cliff"), "El lago no debe conservar el acantilado anterior.");
            Assert.IsNull(route.GetChild(7).Find("Gate"), "La casa no debe conservar la puerta anterior.");
            var terrainColliders = route.Find("Landscape/Terrain").GetComponentsInChildren<MeshCollider>();
            float SurfaceHeight(Vector2 point)
            {
                var ray = new Ray(route.TransformPoint(new Vector3(point.x, 20f, point.y)), Vector3.down);
                foreach (var collider in terrainColliders)
                    if (collider.Raycast(ray, out var hit, 40f)) return route.InverseTransformPoint(hit.point).y;
                Assert.Fail($"Falta terreno bajo el acantilado en local {point:F3}, rayo desde {ray.origin:F3}.");
                return 0f;
            }
            Assert.GreaterOrEqual(SurfaceHeight(RouteTerrain.Plateau) - RouteLayout.Clearing(5).y, 5.99f,
                $"Altura del acantilado sobre el lago en local {RouteTerrain.Plateau:F3}.");
            Assert.Less(SurfaceHeight(RouteTerrain.CliffFoot), 1.5f, $"Pie del acantilado demasiado alto en local {RouteTerrain.CliffFoot:F3}.");
            BakeRouteNavMesh();
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out var start, 0.5f, NavMesh.AllAreas),
                $"Spawn fuera del NavMesh. {RouteDetails("Spawn -> primer claro", player.transform.position, ClearingEntry(0))}");
            Vector3 last = start.position;
            for (int i = 1; i < RouteLayout.Count; i++)
            {
                string label = $"{route.GetChild(i - 1).name} -> {route.GetChild(i).name}";
                Assert.IsTrue(NavMesh.SamplePosition(ClearingEntry(i), out var end, 0.5f, NavMesh.AllAreas),
                    $"Entrada fuera del NavMesh. {RouteDetails(label, last, ClearingEntry(i))}");
                Assert.IsTrue(NavMesh.CalculatePath(last, end.position, NavMesh.AllAreas, path), RouteDetails(label, last, end.position, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, RouteDetails(label, last, end.position, path));
                last = end.position;
            }
            Assert.IsFalse(NavMesh.SamplePosition(gate.position, out _, 0.1f, NavMesh.AllAreas), $"La puerta en {gate.position:F3} no debe ser caminable.");
            Vector3 west = gate.position + Vector3.left * 2f, east = gate.position + Vector3.right * 2f;
            Assert.IsTrue(NavMesh.SamplePosition(west, out var gateWest, 0.5f, NavMesh.AllAreas),
                $"Lado oeste fuera del NavMesh. {RouteDetails("Puerta oeste -> este", west, east)}");
            Assert.IsTrue(NavMesh.SamplePosition(east, out var gateEast, 0.5f, NavMesh.AllAreas),
                $"Lado este fuera del NavMesh. {RouteDetails("Puerta oeste -> este", west, east)}");
            Assert.IsTrue(NavMesh.Raycast(gateWest.position, gateEast.position, out _, NavMesh.AllAreas),
                $"La puerta debe cortar el paso directo. {RouteDetails("Puerta oeste -> este", gateWest.position, gateEast.position)}");
            Assert.IsTrue(NavMesh.CalculatePath(last, start.position, NavMesh.AllAreas, path), RouteDetails("Casa -> spawn", last, start.position, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, RouteDetails("Casa -> spawn", last, start.position, path));
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 delta = corners[i] - corners[i - 1];
                length += delta.magnitude;
                Assert.IsFalse(gate.GetComponent<Collider>().Raycast(new Ray(corners[i - 1] + Vector3.up * 0.6f, delta.normalized),
                    out _, delta.magnitude), $"Esquina {i}: atraviesa la puerta. {RouteDetails("Casa -> spawn", last, start.position, path)}");
            }
            Assert.Greater(length, Vector3.Distance(last, start.position) * 2f,
                $"El regreso de {length:F3} m debe rodear la cerca. {RouteDetails("Casa -> spawn", last, start.position, path)}");
            var terrain = new RouteTerrain();
            int forest = NavMesh.GetAreaFromName(RouteTerrain.ForestArea);
            Assert.Greater(forest, 0, "Falta el área Forest independiente de Walkable.");
            Assert.AreEqual(4f, NavMesh.GetAreaCost(forest), "Costo de Forest en NavMeshAreas.");
            Assert.AreEqual(4f, PlaythroughNavigation.CreateFilter().GetAreaCost(forest), "Costo de Forest en el filtro de la partida automática.");
            Vector3 forestPoint = route.TransformPoint(new Vector3(198f, terrain.GroundHeight(198f, 15f), 15f));
            Assert.IsTrue(NavMesh.SamplePosition(forestPoint, out var woods, 3f, 1 << forest),
                $"Falta NavMesh de Forest. {RouteDetails("Spawn -> bosque", start.position, forestPoint)}");
            Assert.IsTrue(NavMesh.CalculatePath(start.position, woods.position, NavMesh.AllAreas, path), RouteDetails("Spawn -> bosque", start.position, woods.position, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, RouteDetails("Spawn -> bosque", start.position, woods.position, path));
            var tracks = RouteLayout.Clearing(2);
            int walkable = 1 << NavMesh.GetAreaFromName("Walkable");
            Vector3 previous = Vector3.zero;
            for (float z = tracks.z; z <= RouteLayout.TrainNorthEnd; z += 2f)
            {
                Vector3 point = route.TransformPoint(new Vector3(tracks.x, terrain.TrainHeight(z), z));
                string label = $"Puente del tren, z={z:F3}";
                Assert.IsTrue(NavMesh.SamplePosition(point, out var deck, 0.5f, walkable),
                    $"Tablero fuera de Walkable. {RouteDetails(label, previous, point)}");
                if (z > tracks.z)
                {
                    Assert.IsTrue(NavMesh.CalculatePath(previous, deck.position, walkable, path), RouteDetails(label, previous, deck.position, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, RouteDetails(label, previous, deck.position, path));
                }
                previous = deck.position;
            }
            var navigation = new PlaythroughNavigation();
            for (int i = 0; i < RouteLayout.Count - 1; i++)
            {
                Vector3 origin = ClearingEntry(i);
                Vector3 target = ClearingEntry(i + 1);
                string label = $"Sendero {route.GetChild(i).name} -> {route.GetChild(i + 1).name}";
                Assert.IsTrue(NavMesh.SamplePosition(origin, out var from, 0.5f, walkable),
                    $"Origen fuera de Walkable. {RouteDetails(label, origin, target)}");
                Assert.IsTrue(NavMesh.SamplePosition(target, out var to, 0.5f, walkable),
                    $"Destino fuera de Walkable. {RouteDetails(label, origin, target)}");
                Assert.IsTrue(NavMesh.CalculatePath(from.position, to.position, walkable, path), RouteDetails(label, from.position, to.position, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, RouteDetails(label, from.position, to.position, path));
                Assert.IsTrue(navigation.Calculate(origin, target), NavigationDetails(label, origin, target, navigation));
            }
            var hut = route.GetChild(4);
            var memory = hut.GetComponentInChildren<MemorySpot>();
            Assert.IsNotNull(memory, "Falta el recuerdo de la choza.");
            Assert.AreEqual(5, memory.memoryId, "El recuerdo de la choza debe ser el quinto.");
            Vector3 previousStop = ClearingEntry(3);
            string previousName = "Route 4 Stable";
            foreach (var (name, target) in new[] { ("Choza/Family Candle", hut.Find("Family Candle").position + Vector3.back * 1.5f),
                ("Recuerdo 5", memory.transform.position), ("Escondite de la choza", hut.GetComponentInChildren<HideVolume>().HidePoint),
                ("Lago/Family Candle", route.GetChild(5).Find("Family Candle").position + Vector3.back * 1.5f) })
            {
                Assert.IsTrue(navigation.Calculate(previousStop, target), NavigationDetails($"{previousName} -> {name}", previousStop, target, navigation));
                previousStop = target;
                previousName = name;
            }
        }

        Vector3 ClearingEntry(int index) => route.GetChild(index).TransformPoint(
            Vector3.back * (13f * RouteLayout.ClearingSizeOf(index) / RouteLayout.ClearingSize));

        [Test]
        public void LakeHasNoWalkableWaterAndItsBanksBlockEntry()
        {
            var lake = route.GetChild(5);
            var water = lake.Find("Water");
            Assert.IsNull(water.GetComponent<Collider>());
            Assert.Greater(water.GetComponent<Renderer>().bounds.size.x, 100f);
            Assert.Greater(water.GetComponent<Renderer>().bounds.size.z, 60f);
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
            foreach (var renderer in river.GetComponentsInChildren<Renderer>(true))
                Assert.AreNotEqual("Universal Render Pipeline/Tiffany Water", renderer.sharedMaterial.shader.name, renderer.name);
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

        [TestCase(0f)]
        [TestCase(37f)]
        public void RiverKitFitsTheWalkingCollidersInTheCrossingsLocalFrame(float yaw)
        {
            var crossing = new GameObject("Rotated Crossing").transform;
            crossing.SetParent(root.transform, false);
            crossing.SetPositionAndRotation(new Vector3(-30f, 3f, -20f), Quaternion.Euler(0f, yaw, 0f));
            RouteLayout.RiverSpan(out var start, out var end);
            WaterBuilder.BuildRiver(crossing, RouteLayout.PathWidth, Vector3.Distance(start, end),
                GroundMaterials.For(GroundType.Dirt));
            Physics.SyncTransforms();
            var river = crossing.Find("River");
            for (int i = 0; i < 6; i++)
            {
                var piece = river.Find(i < 5 ? $"Stone {i + 1}" : "Log");
                var collider = piece.GetComponent<BoxCollider>();
                Assert.AreEqual(1, piece.GetComponentsInChildren<Collider>().Length);
                Assert.AreEqual(1, piece.GetComponentsInChildren<GroundSurface>().Length);
                var bounds = ModelLibrary.LocalBounds(piece);
                Assert.Less(Vector3.Distance(collider.size, bounds.size), 0.001f);
                Assert.Less(bounds.center.magnitude, 0.001f);
                string model = i < 5 ? $"river-step-{i % 2 + 1}" : "river-log";
                foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
                    Assert.AreEqual($"{ModelLibrary.ModelFolder}/{model}.fbx", AssetDatabase.GetAssetPath(filter.sharedMesh));
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        StringAssert.StartsWith(ModelLibrary.materialFolder + "/", AssetDatabase.GetAssetPath(material));
                Assert.IsTrue(collider.Raycast(new Ray(piece.position + Vector3.up, Vector3.down), out _, 2f));
            }
            int rocks = 0, banks = 0, plants = 0;
            foreach (Transform piece in river)
            {
                if (piece.name.StartsWith("river-rock-"))
                {
                    rocks++;
                    Assert.Greater(Mathf.Abs(piece.localPosition.x) - ModelLibrary.LocalBounds(piece).extents.x, WaterBuilder.CrossingWidth * 0.5f);
                }
                if (piece.name == "river-bed-straight")
                {
                    banks++;
                    Assert.IsEmpty(piece.GetComponentsInChildren<Collider>());
                    Assert.AreEqual(WaterBuilder.RiverWidth * 0.5f, Mathf.Abs(piece.localPosition.z), 0.001f);
                }
                if (piece.name == "plant-tussock") plants++;
            }
            Assert.AreEqual(4, rocks);
            Assert.AreEqual(2, banks);
            Assert.AreEqual(2, plants);
            foreach (var renderer in river.GetComponentsInChildren<Renderer>())
                Assert.AreNotEqual("Universal Render Pipeline/Tiffany Water", renderer.sharedMaterial.shader.name);
        }

        [Test]
        public void WaterHasSavedShaderPositionedLoopsAndLitCaptureStops()
        {
            var lake = route.GetChild(5).Find("Water");
            var river = route.GetChild(5).Find("Path to Next Clearing/River Crossing/River/River Sound");
            foreach (var surface in new[] { lake, route.Find("Landscape/River/Water 1") })
            {
                var material = surface.GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(AssetDatabase.Contains(material));
                Assert.AreEqual("Universal Render Pipeline/Tiffany Water", material.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                Assert.Less(material.GetColor("_BaseColor").a, 1f);
            }
            foreach (var water in new[] { lake, river })
            {
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
            var capture = lakeStop.GetComponent<CaptureStop>();
            var view = new Ray(capture.cameraPosition, capture.cameraRotation * Vector3.forward);
            Assert.IsTrue(new Plane(Vector3.up, lake.position).Raycast(view, out float distance));
            Vector3 focus = route.InverseTransformPoint(view.GetPoint(distance));
            Assert.Less(new RouteTerrain().LakeDistance(focus.x, focus.z), 0f, "La cámara fija encuadra el agua.");
            Assert.IsTrue(HitsRoute(lakeStop.position + Vector3.up, Vector3.down, 1.1f));
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
                if (!modifier.ignoreFromBuild)
                {
                    Assert.IsTrue(modifier.overrideArea, collider.name);
                    Assert.AreEqual(notWalkable, modifier.area, collider.name);
                }
                Assert.IsTrue(modifier.AffectsAgentType(NavMesh.GetSettingsByIndex(0).agentTypeID), collider.name);
                Assert.AreEqual(ChallengeBuilder.IsMoving(collider.transform), modifier.ignoreFromBuild, collider.name);
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
