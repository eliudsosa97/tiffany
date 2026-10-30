using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PosterBuilderTests
    {
        GameObject root;
        Mesh trunk;
        ForestInstanceData data;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Poster Test Route");
            root.transform.SetPositionAndRotation(new Vector3(160f, 2f, -3f), Quaternion.Euler(0f, 31f, 0f));
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk = Object.Instantiate(cylinder.GetComponent<MeshFilter>().sharedMesh);
            trunk.name = "BugambiliasOakPosterTest";
            Object.DestroyImmediate(cylinder);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(trunk);
            if (data != null) Object.DestroyImmediate(data);
        }

        [Test]
        public void CollectsActualTrunksInstancesLampPolesAndBothFenceRuns()
        {
            var terrain = new RouteTerrain();
            var tree = new GameObject("Oak", typeof(MeshFilter));
            tree.transform.SetParent(root.transform, false);
            tree.transform.localPosition = Position(-100f, 1.5f);
            tree.transform.localScale = new Vector3(2.4f, 1.5f, 2.4f);
            tree.GetComponent<MeshFilter>().sharedMesh = trunk;
            var fill = new GameObject("Fill").AddComponent<ForestInstances>();
            fill.enabled = false;
            fill.transform.SetParent(root.transform, false);
            data = ScriptableObject.CreateInstance<ForestInstanceData>();
            data.batches = new[] { new ForestInstanceData.Batch { mesh = trunk,
                matrices = new[] { Matrix4x4.TRS(Position(-80f, 1.5f), Quaternion.Euler(0f, 37f, 0f), new Vector3(2.4f, 1.5f, 2.4f)) } } };
            fill.data = data;
            var lamp = new GameObject("Lamp", typeof(StreetLamp));
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = Position(-60f, 0f);
            Cube("Pole", lamp.transform, Vector3.up * 3.5f, new Vector3(0.25f, 7f, 0.25f));
            var landscape = new GameObject("Landscape").transform;
            landscape.SetParent(root.transform, false);
            var fence = new GameObject("Fence").transform;
            fence.SetParent(landscape, false);
            Cube("Post", fence, Position(-40f, 1.5f), new Vector3(0.25f, 3f, 0.25f));
            var boundary = new GameObject("Town Boundary").transform;
            boundary.SetParent(fence, false);
            Cube("Post", boundary, Position(-20f, 1.5f), new Vector3(0.2f, 3f, 0.2f));
            Cube("Rail", boundary, Position(-10f, 1.5f), Vector3.one);

            var anchors = PosterBuilder.CollectAnchors(root.transform, terrain);
            Assert.AreEqual(5, anchors.Count);
            var counts = new int[3];
            foreach (var anchor in anchors)
            {
                counts[(int)anchor.Kind]++;
                Assert.IsTrue(PosterLayout.Eligible(anchor.Position, terrain));
                Assert.LessOrEqual(Vector3.Angle(anchor.Rotation * Vector3.back, Vector3.back), 10.001f);
                Assert.LessOrEqual(Vector3.Angle(anchor.Rotation * Vector3.up, Vector3.up), 6.001f);
                float x = Mathf.Round(anchor.Position.x / 20f) * 20f;
                Assert.That(anchor.Position.y - terrain.GroundHeight(x, 163f), Is.InRange(1.4f, 1.7f));
                if (anchor.Kind != PosterLayout.AnchorKind.Tree) continue;
                var matrix = Matrix4x4.TRS(Position(x, 1.5f), Quaternion.Euler(0f, x == -80f ? 37f : 0f, 0f), new Vector3(2.4f, 1.5f, 2.4f));
                Assert.That(Clearance(anchor.Position, matrix), Is.InRange(-0.0001f, 0.03f));
                for (int side = -1; side <= 1; side += 2)
                for (int top = -1; top <= 1; top += 2)
                {
                    Vector3 corner = anchor.Position + anchor.Rotation * new Vector3(side * PosterLayout.Width * 0.5f, top * PosterLayout.Height * 0.5f, 0f);
                    Assert.GreaterOrEqual(Clearance(corner, matrix), -0.0001f);
                }
            }
            CollectionAssert.AreEqual(new[] { 2, 1, 2 }, counts);

            Vector3 Position(float x, float height) => new Vector3(x, terrain.GroundHeight(x, 163f) + height, 163f);
        }

        [TestCase("BugambiliasOak0")]
        [TestCase("BugambiliasOak1")]
        [TestCase("Ocote0")]
        public void FindsContactOnTheActualTreeMeshesIncludingOffCenterTrunks(string name)
        {
            var terrain = new RouteTerrain();
            var tree = new GameObject("Tree", typeof(MeshFilter));
            tree.transform.SetParent(root.transform, false);
            tree.transform.localPosition = new Vector3(-100f, terrain.GroundHeight(-100f, 163f), 163f);
            tree.transform.localScale = name == "Ocote0" ? Vector3.one * 0.484f : new Vector3(0.77f, 1.1f, 0.77f);
            tree.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Art/Stylized/Meshes/{name}.asset");
            Assert.IsNotNull(tree.GetComponent<MeshFilter>().sharedMesh);
            int supported = 0;
            for (int yaw = 0; yaw < 360; yaw += 5)
            {
                tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                supported += PosterBuilder.CollectAnchors(root.transform, terrain).Count;
            }
            Assert.Greater(supported, 0, "La superficie real debe aportar anclajes aunque el tronco esté descentrado.");
        }

        [Test]
        public void BuildsOneCaptureStopFacingThePosterNearestTheRoad()
        {
            var terrain = new RouteTerrain();
            foreach (float x in new[] { -100f, -20f })
            {
                var lamp = new GameObject("Lamp", typeof(StreetLamp));
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = new Vector3(x, terrain.GroundHeight(x, 163f), 163f);
                Cube("Pole", lamp.transform, Vector3.up * 3.5f, new Vector3(0.25f, 7f, 0.25f));
            }

            PosterBuilder.Build(root.transform);

            var posters = root.transform.Find("Wanted Posters");
            Assert.IsNotNull(posters);
            var stops = posters.GetComponentsInChildren<CaptureStop>();
            Assert.AreEqual(1, stops.Length);
            var stop = stops[0];
            Assert.AreEqual("Poster 1 Near Road", stop.name);
            Assert.AreEqual(posters, stop.transform.parent);
            Assert.IsTrue(stop.overrideCamera);
            Assert.IsNotNull(stop.GetComponent<LightCaptureStage>());
            var renderers = posters.GetComponentsInChildren<MeshRenderer>();
            Assert.Greater(renderers.Length, 0);
            var selected = posters.GetChild(0);
            float nearest = float.PositiveInfinity;
            foreach (var renderer in renderers)
            {
                var texture = renderer.sharedMaterial.mainTexture;
                Assert.AreEqual(0.6f, renderer.transform.localScale.y);
                Assert.AreEqual(0.6f * texture.width / texture.height, renderer.transform.localScale.x, 0.0001f);
                Assert.AreEqual(0.6f * 711f / 1024f, renderer.transform.localScale.x, 0.0001f);
                Vector3 position = renderer.transform.localPosition;
                float distance = RouteTerrain.RectangleDistance(new Vector2(position.x, position.z), terrain.Road);
                if (distance >= nearest) continue;
                nearest = distance;
                selected = renderer.transform;
            }
            Vector3 toPoster = selected.position - stop.cameraPosition;
            Assert.Less(toPoster.magnitude, 5f);
            Assert.Greater(Vector3.Dot(stop.cameraRotation * Vector3.forward, toPoster), 0f);
            Assert.Less(Vector3.Angle(stop.cameraRotation * Vector3.forward, toPoster), 0.1f);
            Assert.Greater(Vector3.Dot(selected.rotation * Vector3.back, -toPoster), 0f);
        }

        float Clearance(Vector3 point, Matrix4x4 matrix)
        {
            var vertices = trunk.vertices;
            var triangles = trunk.triangles;
            float distance = float.NegativeInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]);
                Vector3 b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, a - matrix.MultiplyPoint3x4(Vector3.zero)) < 0f) normal = -normal;
                distance = Mathf.Max(distance, Vector3.Dot(point - a, normal));
            }
            return distance;
        }

        [Test]
        public void ImporterKeepsPostersCompressedClampedAndMipmapped()
        {
            var importer = AssetImporter.GetAtPath(PosterBuilder.Folder + "/wanted-poster.png") as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.AreEqual(1024, importer.maxTextureSize);
            Assert.AreEqual(TextureImporterCompression.Compressed, importer.textureCompression);
            Assert.IsTrue(importer.mipmapEnabled);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }

        static void Cube(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
        }
    }
}
