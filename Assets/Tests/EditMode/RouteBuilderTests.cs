using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
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
            MemoryBuilder.Build(root.transform, player, null, null, null, memories, TempFolder + "/EchoVolumeProfile.asset");
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
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
        public void FiveMemoriesBelongToTheirClearingsAndBothEchoCapturesUseTheGravestone()
        {
            var collector = player.GetComponent<MemoryCollector>();
            Assert.AreEqual(5, collector.spots.Length);
            for (int i = 0; i < 8; i++)
            {
                var spots = route.GetChild(i).GetComponentsInChildren<MemorySpot>();
                Assert.AreEqual(i < 5 ? 1 : 0, spots.Length);
                if (i >= 5) continue;
                Assert.AreEqual(i + 1, spots[0].memoryId);
                Assert.AreSame(spots[0], collector.spots[i]);
            }
            var grave = route.GetChild(0).Find("Tiffany Gravestone");
            Assert.Less(Vector3.Distance(grave.position, collector.spots[0].transform.position), 2f);
            var ball = route.GetChild(0).Find("Ball");
            Assert.IsNotNull(ball);
            Assert.IsNull(ball.GetComponentInChildren<MemorySpot>());
            var stages = root.GetComponentsInChildren<MemoryCaptureStage>();
            Assert.AreEqual(2, stages.Length);
            Assert.AreEqual("Memory 0 Spot", stages[0].name);
            Assert.AreEqual("Memory 1 Echo", stages[1].name);
            foreach (var stage in stages)
            {
                Assert.AreSame(collector, stage.collector);
                Assert.AreSame(collector.spots[0], stage.spot);
                Assert.Less(Vector3.Distance(stage.transform.position, stage.spot.transform.position), 2f);
                var capture = stage.GetComponent<CaptureStop>();
                Assert.IsTrue(capture.overrideCamera);
                Vector3 toSpot = (stage.spot.transform.position - capture.cameraPosition).normalized;
                Assert.Greater(Vector3.Dot(capture.cameraRotation * Vector3.forward, toSpot), 0.99f);
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
            Assert.AreEqual(8, skyRoute.Length);
            for (int i = 0; i < skyRoute.Length; i++)
            {
                Vector3 offset = skyRoute[i] - route.GetChild(i).position;
                Assert.AreEqual(Vector3.up * 14f, offset);
            }
        }

        [Test]
        public void RouteUsesFewStaticRenderersSavedUrpMaterialsAndOnlyCandleLights()
        {
            Assert.Less(route.GetComponentsInChildren<Transform>(true).Length, 600);
            var renderers = route.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            Assert.Less(renderers.Length, 300);
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

        bool HitsRoute(Vector3 origin, Vector3 direction, float distance)
        {
            foreach (var collider in route.GetComponentsInChildren<Collider>())
                if (!collider.isTrigger && collider.Raycast(new Ray(origin, direction), out _, distance)) return true;
            return false;
        }
    }
}
