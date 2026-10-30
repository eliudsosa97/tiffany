using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchHutModelTests
    {
        [Test]
        public void FbxHasAFloorPivotEightMeterWallsAndFourMeshesWithinSixThousandTriangles()
        {
            string path = ModelLibrary.ModelFolder + "/witch-hut.fbx";
            Assert.IsTrue(File.Exists(path), path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(model);
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            Assert.AreEqual(4, model.GetComponentsInChildren<MeshFilter>(true).Length);
            foreach (string name in new[] { "HutWalls", "HutRoof", "HutInterior", "HutFence" })
            {
                var part = model.transform.Find(name);
                Assert.IsNotNull(part, name);
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
        }

        [Test]
        public void FbxUsesEveryWitchHutMaterialAndOnlyTheEmbersAreUnlit()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelLibrary.ModelFolder + "/witch-hut.fbx");
            Assert.IsNotNull(model);
            var spec = ModelLibrary.LoadSpec("witch-hut");
            var used = new System.Collections.Generic.HashSet<string>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material);
                StringAssert.StartsWith("WitchHut", material.name);
                used.Add(material.name);
            }
            CollectionAssert.AreEquivalent(System.Array.ConvertAll(spec.materials, material => material.name), used);
            foreach (var material in spec.materials)
                Assert.AreEqual(material.name == "WitchHutEmber", material.unlit, material.name);
        }
    }
}
