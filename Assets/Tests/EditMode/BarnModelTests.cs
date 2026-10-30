using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class BarnModelTests
    {
        [Test]
        public void FbxKeepsSixNeutralMeshesCutawayWallsAndSharedMaterialsUnderSixThousandTriangles()
        {
            const string folder = "Assets/BarnModelTestTemp";
            string previous = ModelLibrary.materialFolder;
            var parent = new GameObject(nameof(BarnModelTests));
            try
            {
                string path = ModelLibrary.ModelFolder + "/barn.fbx";
                Assert.IsTrue(File.Exists(path), "Falta exportar barn.fbx con Art/Blender/barn.py.");
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.IsTrue(importer.preserveHierarchy);
                Assert.IsFalse(importer.addCollider || importer.importAnimation || importer.importLights || importer.importCameras);
                ModelLibrary.materialFolder = folder;
                var model = ModelLibrary.Instantiate("barn", parent.transform);
                Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
                Assert.AreEqual(6, model.GetComponentsInChildren<MeshFilter>(true).Length);
                foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
                Assert.That(ModelLibraryTests.TriangleCount(model.transform), Is.InRange(1, 5999));
                var walls = ModelLibrary.LocalBounds(model.transform.Find("BarnWalls"));
                Assert.AreEqual(14.25f, walls.size.x, 0.05f);
                Assert.AreEqual(10.25f, walls.size.z, 0.05f);
                Assert.AreEqual(0f, walls.min.y, 0.001f);
                Assert.LessOrEqual(ModelLibrary.LocalBounds(model.transform.Find("BarnCutaway")).max.y, 1f);
                var used = new HashSet<string>();
                var spec = ModelLibrary.LoadSpec("barn");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    var entry = System.Array.Find(spec.materials, item => item.name == material.name);
                    Assert.IsNotNull(entry);
                    Assert.AreEqual($"{folder}/{entry.name}.mat", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual(GeneratedMaterials.OcclusionShader, material.shader.name);
                    Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                    ColorUtility.TryParseHtmlString(entry.color, out var color);
                    Assert.AreEqual(color, material.GetColor("_BaseColor"));
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 0.0001f);
                    used.Add(material.name);
                }
                CollectionAssert.AreEquivalent(System.Array.ConvertAll(spec.materials, item => item.name), used);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                AssetDatabase.DeleteAsset(folder);
                ModelLibrary.materialFolder = previous;
            }
        }
    }
}
