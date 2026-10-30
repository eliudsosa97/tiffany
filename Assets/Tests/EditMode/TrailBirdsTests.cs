using System;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public class TrailBirdsTests
    {
        GameObject root;
        string tempFolder;
        string previousMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            tempFolder = "Assets/TrailBirdsTestTemp_" + Guid.NewGuid().ToString("N");
            previousMaterialFolder = ModelLibrary.materialFolder;
            ModelLibrary.materialFolder = tempFolder + "/Materials";
            root = new GameObject(nameof(TrailBirdsTests));
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Object.DestroyImmediate(root);
                if (AssetDatabase.IsValidFolder(tempFolder)) AssetDatabase.DeleteAsset(tempFolder);
            }
            finally
            {
                ModelLibrary.materialFolder = previousMaterialFolder;
            }
        }

        [TestCase("trail-bird", 300)]
        [TestCase("trail-perch", 250)]
        public void FbxHasFeetPivotNeutralTransformsAndFitsTriangleBudget(string id, int budget)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelLibrary.ModelFolder}/{id}.fbx");
            Assert.IsNotNull(model, $"Falta exportar {id}.fbx.");
            Assert.That(ModelLibraryTests.TriangleCount(model.transform), Is.InRange(1L, (long)budget));
            Assert.AreEqual(1, model.GetComponentsInChildren<Renderer>(true).Length);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(model.GetComponentsInChildren<Animator>(true));
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
            var bounds = ModelLibrary.LocalBounds(model.transform);
            Assert.AreEqual(0f, bounds.min.y, 0.02f);
            if (id == "trail-bird")
            {
                Assert.That(bounds.size.z, Is.InRange(0.18f, 0.24f));
                Assert.That(bounds.size.y, Is.InRange(0.1f, 0.16f));
                Assert.AreEqual(0f, bounds.center.x, 0.01f);
            }
            else
            {
                var spec = ModelLibrary.LoadSpec(id);
                Assert.AreEqual(3, spec.heightScales.Length);
                foreach (float scale in spec.heightScales) Assert.That(bounds.size.y * scale, Is.InRange(1f, 2.5f));
                Assert.IsTrue(bounds.Contains(spec.perchOffset));
                Assert.Greater(spec.perchOffset.z, spec.trunkCollider.radius);
                Assert.That(spec.trunkCollider.radius, Is.InRange(0.05f, 0.15f));
            }
        }

        [TestCase("trail-bird")]
        [TestCase("trail-perch")]
        public void MaterialsAreSharedLitAssetsWithNoEmission(string id)
        {
            var spec = ModelLibrary.LoadSpec(id);
            foreach (var entry in spec.materials)
            {
                Assert.IsFalse(entry.unlit);
                Assert.IsFalse(entry.spirit);
            }
            if (id == "trail-bird")
            {
                CollectionAssert.AreEqual(new[] { "TrailBirdYellow", "TrailBirdWing", "TrailBirdBeak" },
                    Array.ConvertAll(spec.materials, entry => entry.name));
                Assert.AreEqual("#E8B820", spec.materials[0].color);
            }
            var first = ModelLibrary.Instantiate(id, root.transform);
            var second = ModelLibrary.Instantiate(id, root.transform);
            var renderers = first.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                CollectionAssert.AreEqual(materials, second.GetComponentsInChildren<Renderer>(true)[i].sharedMaterials);
                foreach (var material in materials)
                {
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.IsTrue(AssetDatabase.Contains(material));
                    StringAssert.StartsWith(tempFolder + "/Materials/", AssetDatabase.GetAssetPath(material));
                    Assert.IsFalse(material.IsKeywordEnabled("_EMISSION"));
                    Assert.AreEqual(0f, material.GetColor("_EmissionColor").maxColorComponent);
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void PlacementUsesTerrainOffsetThinPerchCollidersAndMovingBirds(int index)
        {
            root.transform.SetPositionAndRotation(new Vector3(9f, 1000f, -2f), Quaternion.Euler(0f, 37f, 0f));
            var clearing = new GameObject("Clearing").transform;
            clearing.SetParent(root.transform, false);
            clearing.localPosition = RouteLayout.Clearing(index);
            var path = new GameObject("Path to Next Clearing").transform;
            path.SetParent(clearing, false);
            path.localPosition = new Vector3(7f, 0.5f, -9f);
            var terrain = new RouteTerrain();
            var points = TrailBirdLayout.Points(RouteLayout.Path(index), terrain, 713 + index);
            TrailBirds.Place(path, index, terrain, 713 + index);
            Assert.AreEqual(points.Count, path.childCount);
            var spec = ModelLibrary.LoadSpec("trail-perch");
            for (int i = 0; i < path.childCount; i++)
            {
                var group = path.GetChild(i);
                Assert.Less(Vector3.Distance(root.transform.InverseTransformPoint(group.position), points[i].Position), 0.001f);
                var perch = group.Find("trail-perch");
                var bird = group.Find("trail-bird");
                Assert.IsNotNull(bird.GetComponent<TrailBird>());
                Assert.IsEmpty(bird.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(group.GetComponentsInChildren<GroundSurface>(true));
                Assert.IsEmpty(group.GetComponentsInChildren<Animator>(true));
                var colliders = group.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(1, colliders.Length);
                Assert.IsInstanceOf<CapsuleCollider>(colliders[0]);
                Assert.AreSame(perch.gameObject, colliders[0].gameObject);
                Assert.IsFalse(colliders[0].isTrigger);
                var capsule = (CapsuleCollider)colliders[0];
                Assert.AreEqual(1, capsule.direction);
                Assert.AreEqual(spec.trunkCollider.radius, capsule.radius);
                Assert.AreEqual(spec.trunkCollider.height, capsule.height);
                Assert.AreEqual(spec.trunkCollider.center, capsule.center);
                Assert.AreEqual(Vector3.Scale(spec.perchOffset, perch.localScale), bird.localPosition);
                Assert.AreEqual(Vector3.one, bird.localScale);
                if (i + 1 < points.Count)
                {
                    Vector3 direction = root.transform.TransformDirection(points[i + 1].Position - points[i].Position);
                    Assert.Greater(Vector3.Dot(bird.forward, Vector3.ProjectOnPlane(direction, Vector3.up).normalized), 0.999f);
                }
                foreach (var node in perch.GetComponentsInChildren<Transform>(true))
                    Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(node.gameObject, StaticEditorFlags.BatchingStatic));
                foreach (var node in bird.GetComponentsInChildren<Transform>(true))
                    Assert.IsFalse(GameObjectUtility.AreStaticEditorFlagsSet(node.gameObject, StaticEditorFlags.BatchingStatic));
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
                    Assert.IsFalse(renderer.receiveShadows);
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void OnlyFirstTrailHasLitCapturesFacingItsFirstBird(int index)
        {
            root.transform.SetPositionAndRotation(new Vector3(9f, 1000f, -2f), Quaternion.Euler(0f, 37f, 0f));
            var clearing = new GameObject("Clearing").transform;
            clearing.SetParent(root.transform, false);
            clearing.localPosition = RouteLayout.Clearing(index);
            var path = new GameObject("Path to Next Clearing").transform;
            path.SetParent(clearing, false);
            path.localPosition = new Vector3(7f, 0.5f, -9f);
            var terrain = new RouteTerrain();
            TrailBirds.Place(path, index, terrain, 713 + index);
            var captures = path.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(index == 0 ? 2 : 0, captures.Length);
            if (index != 0) return;

            CollectionAssert.AreEquivalent(new[] { "Trail Bird 1", "Trail Bird 2" }, Array.ConvertAll(captures, capture => capture.name));
            var bird = path.Find("Trail Bird 1/trail-bird");
            foreach (var capture in captures)
            {
                Assert.AreSame(bird.parent, capture.transform.parent);
                Assert.IsTrue(capture.overrideCamera);
                var light = capture.GetComponent<LightCaptureStage>();
                Assert.IsNotNull(light);
                Assert.AreEqual(SoulEnergy.Max, light.energy);
                Vector3 toBird = bird.position - capture.cameraPosition;
                Assert.Greater(Vector3.Dot(capture.cameraRotation * Vector3.forward, toBird.normalized), 0.9999f);
                Assert.AreEqual(capture.name == "Trail Bird 1" ? 36f : 4f, toBird.magnitude, 0.001f);
                Vector3 facing = Vector3.ProjectOnPlane(bird.position - capture.transform.position, Vector3.up);
                Assert.AreEqual(3f, facing.magnitude, 0.001f);
                Assert.Greater(Vector3.Dot(capture.transform.forward, facing.normalized), 0.9999f);
                Vector3 player = root.transform.InverseTransformPoint(capture.transform.position);
                Assert.IsTrue(terrain.IsWalkable(player.x, player.z));
                Assert.AreEqual(terrain.Height(player.x, player.z), player.y, 0.001f);
                var polyline = RouteLayout.Path(0);
                Assert.LessOrEqual(RouteTerrain.SegmentDistance(new Vector2(player.x, player.z),
                    new Vector2(polyline[0].x, polyline[0].z), new Vector2(polyline[1].x, polyline[1].z)), RouteLayout.PathWidth * 0.5f);
            }
            var trail = Array.Find(captures, capture => capture.name == "Trail Bird 1");
            Assert.Less(Quaternion.Angle(root.transform.rotation * Quaternion.Euler(42f, 45f, 0f), trail.cameraRotation), 0.1f);
        }

        [Test]
        public void ApplyMatchesTheSeededLogicRelativeToTheRestPoseWithoutDrift()
        {
            var bird = ModelLibrary.Instantiate("trail-bird", root.transform).AddComponent<TrailBird>();
            bird.seed = 713;
            Vector3 rest = new Vector3(2f, 1.5f, -3f);
            Quaternion rotation = Quaternion.Euler(0f, 73f, 0f);
            bird.transform.SetLocalPositionAndRotation(rest, rotation);
            var bob = new BirdBob(713);
            for (int i = 0; i < 1800; i++)
            {
                var pose = bob.Tick(1f / 60f);
                bird.Apply(1f / 60f);
                Assert.Less(Vector3.Distance(rest + rotation * pose.Offset, bird.transform.localPosition), 0.00001f);
                Assert.Less(Quaternion.Angle(rotation * pose.Rotation, bird.transform.localRotation), 0.1f);
            }
        }
    }

    public sealed partial class RouteBuilderTests
    {
        [Test]
        public void TrailBirdCapturesFollowAllRouteAndMemoryEchoCaptures()
        {
            var birds = route.GetChild(0).Find("Path to Next Clearing").GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(2, birds.Length);
            var captures = root.GetComponentsInChildren<CaptureStop>();
            foreach (var bird in birds)
                foreach (var capture in captures)
                    if (capture.name.StartsWith("Route ") || capture.GetComponent<EchoCaptureStage>() != null)
                        Assert.Greater(AutoCapture.CompareStops(bird, capture), 0, $"{bird.name}, {capture.name}");
            Assert.Less(AutoCapture.CompareStops(birds[0], birds[1]), 0);
        }

        [Test]
        public void TrailBirdsFitClearingRendererGeometryAndSurroundingForestBudgets()
        {
            root.transform.position += Vector3.up * 1000f;
            GroundLitterBuilder.Scatter(root.transform);
            var litter = root.transform.Find("Ground Litter").GetComponent<ForestInstances>();
            var cells = route.Find("Landscape/Forest Fill").GetComponentsInChildren<ForestInstances>();
            var allBirds = route.GetComponentsInChildren<TrailBird>(true);
            for (int index = 0; index < RouteLayout.Count; index++)
            {
                var clearing = route.GetChild(index);
                var birds = clearing.GetComponentsInChildren<TrailBird>(true);
                int expected = index < RouteLayout.Count - 1
                    ? TrailBirdLayout.Points(RouteLayout.Path(index), new RouteTerrain(), 713 + index).Count : 0;
                Assert.AreEqual(expected, birds.Length);
                int addedRenderers = 0;
                long addedTriangles = 0;
                foreach (var bird in birds)
                {
                    addedRenderers += bird.transform.parent.GetComponentsInChildren<Renderer>(true).Length;
                    addedTriangles += ModelLibraryTests.TriangleCount(bird.transform.parent);
                    foreach (var renderer in bird.GetComponentsInChildren<Renderer>(true))
                        Assert.IsFalse(GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic));
                }
                int renderers = clearing.GetComponentsInChildren<Renderer>(true).Length;
                long triangles = ForestBuilder.TriangleCount(clearing);
                TestContext.WriteLine($"{clearing.name}: {birds.Length} pájaros, renderers {renderers - addedRenderers} -> {renderers}, triángulos {triangles - addedTriangles} -> {triangles}.");
                Assert.AreEqual(birds.Length * 2, addedRenderers);
                Assert.LessOrEqual(addedTriangles, birds.Length * 550L);
                Assert.LessOrEqual(renderers, 200, clearing.name);
                Assert.LessOrEqual(triangles, 150000L, clearing.name);
                Rect view = ForestBuilder.ViewSquare(index);
                long areaTriangles = ForestBuilder.TriangleCount(clearing.Find("Forest"));
                foreach (var cell in cells)
                {
                    Vector3 position = cell.transform.localPosition;
                    var key = new Vector2Int(Mathf.RoundToInt(position.x / ForestBuilder.CellSize), Mathf.RoundToInt(position.z / ForestBuilder.CellSize));
                    if (view.Overlaps(ForestBuilder.CellBounds(key))) areaTriangles += ForestBuilder.TriangleCount(cell.transform);
                }
                foreach (var batch in litter.data.batches)
                {
                    var bounds = ForestInstances.TransformBounds(batch.bounds, route.worldToLocalMatrix * litter.transform.localToWorldMatrix);
                    if (view.Overlaps(Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z)))
                        areaTriangles += ModelLibraryTests.TriangleCount(batch.mesh) * batch.matrices.Length;
                }
                foreach (var bird in allBirds)
                {
                    Vector3 position = route.InverseTransformPoint(bird.transform.parent.position);
                    if (view.Contains(new Vector2(position.x, position.z))) areaTriangles += ModelLibraryTests.TriangleCount(bird.transform.parent);
                }
                TestContext.WriteLine($"{clearing.name}: {areaTriangles} triángulos en 80 x 80 m.");
                Assert.LessOrEqual(areaTriangles, ForestBuilder.AreaTriangleBudget, clearing.name);
            }
        }
    }
}
