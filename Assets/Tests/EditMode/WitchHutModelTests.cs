using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchHutModelTests
    {
        const string TempFolder = "Assets/WitchHutModelTestTemp";
        GameObject parent;
        string previousMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            previousMaterialFolder = ModelLibrary.materialFolder;
            ModelLibrary.materialFolder = TempFolder;
            parent = new GameObject(nameof(WitchHutModelTests));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = previousMaterialFolder;
        }

        [Test]
        public void FbxHasAFloorPivotEightMeterWallsAndFourMeshesWithinSixThousandTriangles()
        {
            string path = ModelLibrary.ModelFolder + "/witch-hut.fbx";
            Assert.IsTrue(File.Exists(path), "Falta exportar witch-hut.fbx con Art/Blender/witch-hut.py.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(model);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.preserveHierarchy);
            Assert.IsFalse(importer.addCollider || importer.importLights || importer.importCameras || importer.importAnimation);
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(model.GetComponentsInChildren<Light>(true));
            Assert.AreEqual(4, model.transform.childCount);
            Assert.AreEqual(4, model.GetComponentsInChildren<MeshFilter>(true).Length);
            foreach (string name in new[] { "HutWalls", "HutRoof", "HutInterior", "HutFence" })
            {
                var part = model.transform.Find(name);
                Assert.IsNotNull(part, name);
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++) Assert.AreEqual(MeshTopology.Triangles, mesh.GetTopology(i));
                long triangles = ModelLibraryTests.TriangleCount(part);
                Assert.Greater(triangles, 0, name);
                TestContext.WriteLine($"{name}: {triangles} triángulos");
            }
            Assert.That(ModelLibraryTests.TriangleCount(model.transform), Is.InRange(1, 6000));
            var walls = ModelLibrary.LocalBounds(model.transform.Find("HutWalls"));
            Assert.That(walls.size.x, Is.InRange(8f, 9f));
            Assert.That(walls.size.z, Is.InRange(8f, 9f));
            Assert.That(walls.max.y, Is.InRange(2.2f, 2.7f));
            var bounds = ModelLibrary.LocalBounds(model.transform);
            Assert.AreEqual(0f, bounds.min.y, 0.02f);
            Assert.Less(new Vector2(bounds.center.x, bounds.center.z).magnitude, 0.2f);
            Assert.That(bounds.size.x, Is.InRange(10f, 11f));
            Assert.That(bounds.size.z, Is.InRange(10f, 11f));
            Assert.That(bounds.max.y, Is.InRange(4.4f, 4.8f));
        }

        [Test]
        public void FbxUsesEveryWitchHutMaterialAndOnlyTheEmbersAreUnlit()
        {
            var model = ModelLibrary.Instantiate("witch-hut", parent.transform);
            var spec = ModelLibrary.LoadSpec("witch-hut");
            var used = new HashSet<string>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material);
                StringAssert.StartsWith("WitchHut", material.name);
                Assert.AreEqual($"{TempFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                Assert.AreEqual(material.name == "WitchHutEmber" ? "Universal Render Pipeline/Unlit"
                    : GeneratedMaterials.OcclusionShader, material.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                Assert.IsTrue(ColorUtility.TryParseHtmlString(System.Array.Find(spec.materials,
                    entry => entry.name == material.name).color, out var expected));
                for (int i = 0; i < 4; i++) Assert.AreEqual(expected[i], material.GetColor("_BaseColor")[i], 0.0001f);
                used.Add(material.name);
            }
            CollectionAssert.AreEquivalent(System.Array.ConvertAll(spec.materials, material => material.name), used);
            foreach (var material in spec.materials)
                Assert.AreEqual(material.name == "WitchHutEmber", material.unlit, material.name);
        }

        [Test]
        public void FbxHasAnOpenFourMeterDoorInPositiveZAndSolidWalls()
        {
            var model = ModelLibrary.Instantiate("witch-hut", parent.transform);
            var walls = model.transform.Find("HutWalls").gameObject.AddComponent<MeshCollider>();
            walls.sharedMesh = walls.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            foreach (float x in new[] { -1.5f, 0f, 1.5f })
                Assert.IsFalse(walls.Raycast(new Ray(new Vector3(x, 1f, 6f), Vector3.back), out _, 7f));
            foreach (float x in new[] { -3f, 3f })
                Assert.IsTrue(walls.Raycast(new Ray(new Vector3(x, 1f, 6f), Vector3.back), out _, 3f));
            Assert.IsTrue(walls.Raycast(new Ray(new Vector3(0f, 1f, -6f), Vector3.forward), out _, 3f));
        }
    }
}
