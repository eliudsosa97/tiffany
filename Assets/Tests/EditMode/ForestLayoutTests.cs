using System.Collections.Generic;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class ForestLayoutTests
    {
        const string TempFolder = "Assets/ForestLayoutTestTemp";
        GameObject root;
        string previousMaterialFolder;
        string previousKenneyMaterialRoot;

        [SetUp]
        public void SetUp()
        {
            previousMaterialFolder = ModelLibrary.materialFolder;
            previousKenneyMaterialRoot = ModelLibrary.kenneyMaterialRoot;
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            ModelLibrary.kenneyMaterialRoot = TempFolder + "/Kenney";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = previousMaterialFolder;
            ModelLibrary.kenneyMaterialRoot = previousKenneyMaterialRoot;
        }

        [Test]
        public void SampleClearsPatchesSpawnBordersAndInitialCameraView()
        {
            var scales = new Dictionary<string, HashSet<float>>();
            var rotations = new Dictionary<string, HashSet<float>>();
            var counts = new Dictionary<string, int>();
            Assert.AreEqual(12, ForestLayout.Placements.Count);
            foreach (var placement in ForestLayout.Placements)
            {
                Assert.IsTrue(ForestLayout.IsClear(placement.x, placement.alongSlope, placement.Radius), placement.id);
                Assert.Greater(placement.alongSlope - placement.Radius, 0f, placement.id);
                Assert.That(placement.scale, Is.InRange(0.8f, 1.2f));
                if (placement.IsPine) Assert.That(placement.height * placement.scale, Is.InRange(9f, 12f));
                string type = placement.IsPine ? "pine"
                    : placement.id.StartsWith("Nature/rock_") || placement.id.StartsWith("Nature/stone_") ? "rock" : placement.id;
                if (!scales.ContainsKey(type))
                {
                    scales[type] = new HashSet<float>();
                    rotations[type] = new HashSet<float>();
                    counts[type] = 0;
                }
                scales[type].Add(placement.scale);
                rotations[type].Add(placement.yaw);
                counts[type]++;
            }
            Assert.AreEqual(3, counts["pine"]);
            Assert.AreEqual(2, counts["oak"]);
            Assert.AreEqual(2, counts["bougainvillea"]);
            Assert.AreEqual(3, counts["rock"]);
            Assert.AreEqual(1, counts["Nature/log"]);
            Assert.AreEqual(1, counts["Nature/stump_oldTall"]);
            foreach (string type in scales.Keys)
            {
                if (counts[type] == 1) continue;
                Assert.GreaterOrEqual(scales[type].Count, 2, type);
                Assert.GreaterOrEqual(rotations[type].Count, 2, type);
            }
        }

        [Test]
        public void SamplesDoNotOverlapEachOther()
        {
            var placements = ForestLayout.Placements;
            for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                var a = placements[i];
                var b = placements[j];
                Assert.Greater(Vector2.Distance(new Vector2(a.x, a.alongSlope), new Vector2(b.x, b.alongSlope)),
                    a.Radius + b.Radius, $"{a.id} {i}, {b.id} {j}");
            }
        }

        [TestCase(-15f, -9f, 0f)]
        [TestCase(0f, -9f, 0f)]
        [TestCase(15f, -9f, 0f)]
        [TestCase(-15f, 9f, 0f)]
        [TestCase(0f, 9f, 0f)]
        [TestCase(15f, 9f, 0f)]
        [TestCase(21f, 9f, 1.1f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(3.9f, 0f, 0f)]
        [TestCase(4.5f, 0f, 0.6f)]
        [TestCase(29f, 0f, 0f)]
        [TestCase(-29f, 0f, 0f)]
        [TestCase(0f, 29f, 0f)]
        [TestCase(0f, -29f, 0f)]
        [TestCase(27.5f, 20f, 0.6f)]
        public void ReservedSpaceIsNotClear(float x, float alongSlope, float radius)
        {
            Assert.IsFalse(ForestLayout.IsClear(x, alongSlope, radius));
        }

        [TestCase(25f, 20f, 1f)]
        [TestCase(-7.5f, 8f, 2f)]
        [TestCase(7.5f, -8f, 2f)]
        [TestCase(0f, 20f, 4f)]
        public void OpenSpaceIsClear(float x, float alongSlope, float radius)
        {
            Assert.IsTrue(ForestLayout.IsClear(x, alongSlope, radius));
        }

        [Test]
        public void BuilderCreatesCoverTrunksRocksAndEnterableHideVolumes()
        {
            root = new GameObject("ForestLayoutTests");
            SceneBuilder.BuildForestSample(root.transform);
            Physics.SyncTransforms();
            Assert.AreEqual(8, LayerMask.NameToLayer(ForestLayout.CoverLayer));
            Assert.AreEqual(ForestLayout.Placements.Count, root.transform.childCount);
            for (int i = 0; i < ForestLayout.Placements.Count; i++)
            {
                var placement = ForestLayout.Placements[i];
                var piece = root.transform.GetChild(i);
                var spec = placement.IsKenney ? null : ModelLibrary.LoadSpec(placement.id);
                float slope = 8f * Mathf.Deg2Rad;
                Assert.Less(Vector3.Distance(new Vector3(placement.x, placement.alongSlope * Mathf.Sin(slope),
                    placement.alongSlope * Mathf.Cos(slope)), piece.localPosition), 1e-4f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0, placement.yaw, 0), piece.localRotation), 0.01f);
                Assert.AreEqual(Vector3.one * placement.scale, piece.localScale);
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
                {
                    Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                    Assert.IsTrue(renderer.receiveShadows);
                }
                var canopy = piece.Find("Canopy");
                if (placement.IsPine || placement.id == "oak") Assert.IsNotNull(canopy, placement.id);
                if (canopy != null)
                {
                    var collider = canopy.GetComponent<BoxCollider>();
                    Assert.AreEqual(8, canopy.gameObject.layer);
                    Assert.IsFalse(collider.isTrigger);
                    if (!placement.IsKenney)
                        Assert.AreEqual(canopy.GetComponent<MeshFilter>().sharedMesh.bounds.size, collider.size);
                    Assert.Greater(collider.bounds.min.y - collider.bounds.max.z * Mathf.Tan(slope), 2.5f);
                    var origin = new Vector3(collider.bounds.center.x, piece.position.y + 2f, collider.bounds.center.z);
                    Assert.IsTrue(collider.Raycast(new Ray(origin, Vector3.up), out _, 40f));
                    var trunk = placement.IsPine ? piece.GetComponent<CapsuleCollider>() : piece.Find("Trunk").GetComponent<CapsuleCollider>();
                    Assert.IsFalse(trunk.isTrigger);
                    Assert.AreEqual(0, trunk.gameObject.layer);
                    Assert.AreEqual(1, trunk.direction);
                    if (placement.IsPine)
                    {
                        Assert.Greater(trunk.radius, 0.1f);
                        Assert.AreEqual(0f, trunk.center.y - trunk.height * 0.5f, 1e-4f);
                        Assert.GreaterOrEqual(trunk.bounds.max.y, collider.bounds.min.y);
                    }
                    else
                    {
                        Assert.AreEqual(spec.trunkCollider.center, trunk.center);
                        Assert.AreEqual(spec.trunkCollider.radius, trunk.radius);
                        Assert.AreEqual(spec.trunkCollider.height, trunk.height);
                    }
                }
                var bush = piece.Find("Bush");
                if (bush != null)
                {
                    var colliders = bush.GetComponentsInChildren<Collider>();
                    Assert.AreEqual(1, colliders.Length);
                    Assert.IsTrue(colliders[0].isTrigger);
                    var hide = bush.Find("HideVolume");
                    Assert.IsNotNull(hide);
                    Assert.AreEqual(LayerMask.NameToLayer("Ignore Raycast"), hide.gameObject.layer);
                    var volume = hide.GetComponent<BoxCollider>();
                    Assert.AreEqual(spec.hideVolume.center, volume.center);
                    Assert.AreEqual(spec.hideVolume.size, volume.size);
                    Assert.Greater(volume.size.y * placement.scale, 1.2f);
                    Assert.Greater(Mathf.Min(volume.size.x, volume.size.z) * placement.scale, 0.5f);
                }
                if (placement.IsKenney && !placement.IsPine)
                {
                    var collider = piece.GetComponent<BoxCollider>();
                    Assert.IsFalse(collider.isTrigger);
                    Assert.AreEqual(0, piece.gameObject.layer);
                    Assert.AreEqual(1, piece.GetComponentsInChildren<Collider>().Length);
                    var bounds = ModelLibrary.LocalBounds(piece);
                    Assert.Less(Vector3.Distance(bounds.center, collider.center), 1e-4f);
                    Assert.Less(Vector3.Distance(bounds.size, collider.size), 1e-4f);
                }
            }
        }

        [Test]
        public void BakedForestHasNoTrianglesAboveTwoAndAHalfMetersAndDetectsMissingLayerMask()
        {
            var otherSurfaces = new List<NavMeshSurface>(NavMeshSurface.activeSurfaces);
            foreach (var other in otherSurfaces) other.RemoveData();
            NavMeshSurface surface = null;
            try
            {
                root = new GameObject("ForestNavMeshTests");
                var ground = new GameObject("Hillside");
                ground.transform.SetParent(root.transform, false);
                ground.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                ground.transform.localPosition = -ground.transform.up * 0.5f;
                ground.AddComponent<BoxCollider>().size = new Vector3(60f, 1f, 60f);
                SceneBuilder.BuildForestSample(root.transform);
                SceneBuilder.BakeNavMesh(root.transform, TempFolder + "/NavMesh.asset");
                surface = root.GetComponent<NavMeshSurface>();
                Assert.LessOrEqual(MaxTriangleHeightAboveSlope(), 2.5f,
                    "El NavMesh tiene triángulos sobre las copas o por encima del espacio caminable.");
                surface.layerMask = ~0;
                surface.BuildNavMesh();
                Assert.Greater(MaxTriangleHeightAboveSlope(), 2.5f,
                    "El control sin filtro debe hornear las caras superiores de los colliders de copa.");
            }
            finally
            {
                if (surface != null)
                {
                    surface.RemoveData();
                    if (!AssetDatabase.Contains(surface.navMeshData)) Object.DestroyImmediate(surface.navMeshData);
                }
                foreach (var other in otherSurfaces) if (other != null) other.AddData();
            }
        }

        static float MaxTriangleHeightAboveSlope()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            Assert.IsNotEmpty(triangulation.indices, "El horneado debe producir suelo navegable.");
            float maximum = float.NegativeInfinity;
            foreach (int index in triangulation.indices)
            {
                var vertex = triangulation.vertices[index];
                maximum = Mathf.Max(maximum, vertex.y - vertex.z * Mathf.Tan(8f * Mathf.Deg2Rad));
            }
            return maximum;
        }
    }
}
