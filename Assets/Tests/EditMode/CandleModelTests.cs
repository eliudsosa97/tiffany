using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class CandleModelTests
    {
        const string TempFolder = "Assets/CandleModelTestTemp";
        static readonly string[] Variants = { "CandleFamily", "CandleKindled" };
        GameObject parent;

        [SetUp]
        public void SetUp()
        {
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            parent = new GameObject("CandleModelTests");
            parent.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            parent.transform.localScale = new Vector3(2f, 3f, 4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        [Test]
        public void FbxHasBothCandlesWithSeparateFlamesAndNeutralTransforms()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelLibrary.ModelFolder + "/candle.fbx");
            Assert.IsNotNull(model, "Genera candle.fbx con Art/Blender/candle.py antes de ejecutar estos tests.");
            Assert.AreEqual(2, model.transform.childCount);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
                ModelLibraryTests.AssertNeutral(transform);
            foreach (string variant in Variants)
            {
                var body = model.transform.Find(variant);
                Assert.IsNotNull(body, variant);
                Assert.AreSame(model.transform, body.parent);
                Assert.AreEqual(1, body.childCount, variant);
                var flame = body.Find("Flame");
                Assert.IsNotNull(flame, variant);
                Assert.IsNotNull(flame.GetComponent<MeshRenderer>(), variant);
                Assert.IsNotNull(flame.GetComponent<MeshFilter>(), variant);
                Assert.That(ModelLibraryTests.TriangleCount(body), Is.InRange(1, 300), variant);
                var bounds = ModelLibraryTests.LocalBounds(model.transform, variant);
                Assert.AreEqual(0f, bounds.min.y, 0.0001f, variant);
                Assert.Less(new Vector2(bounds.center.x, bounds.center.z).magnitude, 0.003f, variant);
                foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
                    Assert.GreaterOrEqual(filter.sharedMesh.bounds.min.y, -0.0001f, filter.name);
            }
            var family = ModelLibraryTests.LocalBounds(model.transform, "CandleFamily");
            Assert.That(family.size.y, Is.InRange(0.15f, 0.25f));
            Assert.That(family.size.x, Is.InRange(0.055f, 0.065f));
            Assert.That(family.size.z, Is.InRange(0.055f, 0.065f));
            Assert.Less(ModelLibraryTests.LocalBounds(model.transform, "CandleKindled").size.y, family.size.y);
        }

        [Test]
        public void JsonMaterialsHaveTheRequestedShadersAndSurfaces()
        {
            ModelLibrary.SyncMaterials("candle");
            var spec = ModelLibrary.LoadSpec("candle");
            CollectionAssert.IsSubsetOf(new[] { "CandleGlass", "CandleWax", "CandleFlame" },
                System.Array.ConvertAll(spec.materials, m => m.name));
            foreach (var entry in spec.materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{ModelLibrary.materialFolder}/{entry.name}.mat");
                Assert.IsNotNull(material, entry.name);
                Assert.AreEqual("Universal Render Pipeline/" + (entry.unlit ? "Unlit" : "Lit"), material.shader.name);
                Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var color), entry.name);
                var actual = material.GetColor("_BaseColor");
                for (int i = 0; i < 4; i++) Assert.AreEqual(color[i], actual[i], 1e-4f, entry.name);
                bool transparent = entry.surface == "transparent";
                Assert.AreEqual(entry.name == "CandleGlass", transparent, entry.name);
                Assert.AreEqual(entry.name == "CandleFlame", entry.unlit, entry.name);
                Assert.AreEqual(transparent ? 1f : 0f, material.GetFloat("_Surface"), entry.name);
                Assert.AreEqual(0f, material.GetFloat("_AlphaClip"), entry.name);
                if (!entry.unlit)
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f, entry.name);
                if (transparent)
                {
                    Assert.AreEqual("CandleGlass", entry.name);
                    Assert.That(actual.a, Is.InRange(0.01f, 0.35f));
                    Assert.AreEqual(0f, material.GetFloat("_Blend"));
                    Assert.AreEqual(0f, material.GetFloat("_ZWrite"));
                    Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, material.GetFloat("_DstBlend"));
                    Assert.AreEqual((int)RenderQueue.Transparent, material.renderQueue);
                    Assert.AreEqual("Transparent", material.GetTag("RenderType", false));
                    Assert.IsTrue(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                }
                else
                {
                    Assert.IsEmpty(material.shaderKeywords, entry.name);
                    Assert.AreEqual(1f, material.GetFloat("_ZWrite"), entry.name);
                }
            }
        }

        [Test]
        public void InstantiatedFlamesUseAssetsWithoutShadowsOrColliders()
        {
            var root = ModelLibrary.Instantiate("candle", parent.transform);
            Assert.AreEqual("candle", root.name);
            Assert.AreSame(parent.transform, root.transform.parent);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                ModelLibraryTests.AssertNeutral(transform);
            var used = new HashSet<string>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(4, renderers.Length);
            foreach (string variant in Variants)
            {
                var flame = root.transform.Find(variant + "/Flame");
                Assert.IsNotNull(flame, variant);
                var renderer = flame.GetComponent<Renderer>();
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.IsFalse(renderer.receiveShadows);
                Assert.AreEqual(1, renderer.sharedMaterials.Length);
                Assert.AreEqual("CandleFlame", renderer.sharedMaterial.name);
            }
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    used.Add(material.name);
                }
            CollectionAssert.AreEquivalent(System.Array.ConvertAll(ModelLibrary.LoadSpec("candle").materials, m => m.name), used);
        }

        [Test]
        public void SyncedMaterialsAreStableAfterReimport()
        {
            ModelLibrary.SyncMaterials("candle");
            var bytes = new Dictionary<string, byte[]>();
            foreach (var entry in ModelLibrary.LoadSpec("candle").materials)
            {
                string path = $"{ModelLibrary.materialFolder}/{entry.name}.mat";
                bytes[path] = File.ReadAllBytes(path);
                Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<Material>(path));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            ModelLibrary.SyncMaterials("candle");
            foreach (var pair in bytes) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
        }

        [Test]
        public void ExistingWitchMaterialsStayByteIdentical()
        {
            GeneratedMaterials.EnsureFolder(ModelLibrary.materialFolder);
            var bytes = new Dictionary<string, byte[]>();
            foreach (var entry in ModelLibrary.LoadSpec("witch").materials)
            {
                string source = $"{ModelLibrary.MaterialFolder}/{entry.name}.mat";
                string copy = $"{ModelLibrary.materialFolder}/{entry.name}.mat";
                bytes[copy] = File.ReadAllBytes(source);
                Assert.IsTrue(AssetDatabase.CopyAsset(source, copy));
            }
            ModelLibrary.SyncMaterials("candle");
            ModelLibrary.SyncMaterials("witch");
            foreach (var pair in bytes) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
        }
    }
}
