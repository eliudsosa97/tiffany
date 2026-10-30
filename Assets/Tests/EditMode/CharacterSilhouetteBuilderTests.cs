using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class CharacterSilhouetteBuilderTests
    {
        const string Temp = "Assets/CharacterSilhouetteTestTemp";
        GameObject root;
        string previousFolder;

        [SetUp]
        public void SetUp()
        {
            previousFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = Temp;
            root = new GameObject("Character");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(Temp);
            GeneratedMaterials.folder = previousFolder;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddsOneSavedPassWithoutAddingRenderersOrCuttingTheCharacter(bool unlit)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform);
            var renderer = body.GetComponent<Renderer>();
            var material = GeneratedMaterials.Get("Body", Color.cyan, unlit);
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long id));
            renderer.sharedMaterial = material;
            body.SetActive(false);
            Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(root.transform));
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            GeneratedMaterials.Get("Body", Color.cyan, unlit);
            Assert.IsFalse(material.IsKeywordEnabled("_TIFFANY_CHARACTER"));
            Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(root.transform));

            Assert.AreEqual(1, root.GetComponentsInChildren<Renderer>(true).Length);
            Assert.AreSame(mesh, body.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            material = renderer.sharedMaterials[0];
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string actualGuid, out long actualId));
            Assert.AreEqual(guid, actualGuid);
            Assert.AreEqual(id, actualId);
            Assert.AreEqual(Color.cyan, material.GetColor("_BaseColor"));
            Assert.IsTrue(material.IsKeywordEnabled("_TIFFANY_CHARACTER"));
            Assert.AreEqual(unlit, material.IsKeywordEnabled("_TIFFANY_UNLIT"));
            Assert.AreEqual(1f, material.GetFloat("_TiffanyStencil"));
            var silhouette = renderer.sharedMaterials[1];
            Assert.IsTrue(AssetDatabase.Contains(silhouette));
            Assert.AreEqual(GeneratedMaterials.SilhouetteShader, silhouette.shader.name);
            Assert.AreEqual(1, silhouette.passCount);
            Assert.Greater(silhouette.renderQueue, material.renderQueue);
            Assert.GreaterOrEqual(silhouette.FindPass("Silhouette"), 0);
        }

        [Test]
        public void ParticlesAndLightsDoNotReceiveSilhouettes()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform);
            body.GetComponent<Renderer>().sharedMaterial = GeneratedMaterials.Get("Body", Color.white);
            var rain = new GameObject("Rain", typeof(ParticleSystem));
            rain.transform.SetParent(root.transform);
            var light = new GameObject("Soul Light", typeof(Light));
            light.transform.SetParent(root.transform);
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.transform.SetParent(light.transform);
            var effect = GeneratedMaterials.Get("Effect", Color.yellow, unlit: true);
            var effects = new[] { rain.GetComponent<Renderer>(), halo.GetComponent<Renderer>() };
            foreach (var renderer in effects) renderer.sharedMaterial = effect;
            rain.SetActive(false);

            Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(root.transform));

            Assert.AreEqual(2, body.GetComponent<Renderer>().sharedMaterials.Length);
            foreach (var renderer in effects)
            {
                Assert.AreEqual(1, renderer.sharedMaterials.Length, renderer.name);
                Assert.AreEqual("Universal Render Pipeline/Unlit", renderer.sharedMaterial.shader.name);
                Assert.IsFalse(renderer.sharedMaterial.IsKeywordEnabled("_TIFFANY_CHARACTER"));
            }
        }

        [TestCase("tyler")]
        [TestCase("tiffany-provisional")]
        public void AnimatedFigureKeepsItsSkinAndSpiritMaterialsWithOneCompleteSavedSilhouette(string model)
        {
            string previous = ModelLibrary.materialFolder;
            try
            {
                ModelLibrary.materialFolder = Temp;
                var figure = ModelLibrary.Instantiate(model, root.transform);
                var skin = figure.GetComponentInChildren<SkinnedMeshRenderer>();
                var source = skin.sharedMesh;
                var bones = skin.bones;
                var bounds = skin.localBounds;
                var materials = skin.sharedMaterials;
                var spec = ModelLibrary.LoadSpec(model);

                Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(figure.transform));
                AssetDatabase.SaveAssets();
                var materialFiles = new Dictionary<string, byte[]>();
                foreach (var material in skin.sharedMaterials)
                {
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    materialFiles[materialPath] = File.ReadAllBytes(materialPath);
                }
                ModelLibrary.SyncMaterials(model);
                foreach (var material in materials) Assert.AreEqual(0f, material.GetFloat("_TiffanyStencil"));
                Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(figure.transform));
                AssetDatabase.SaveAssets();
                foreach (var file in materialFiles) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);

                Assert.AreEqual(1, figure.GetComponentsInChildren<Renderer>(true).Length);
                Assert.AreEqual(source.subMeshCount + 1, skin.sharedMesh.subMeshCount);
                Assert.AreEqual(materials.Length + 1, skin.sharedMaterials.Length);
                Assert.IsTrue(AssetDatabase.Contains(skin.sharedMesh));
                Assert.AreEqual(source.name + "Silhouette", skin.sharedMesh.name);
                CollectionAssert.AreEqual(source.triangles, skin.sharedMesh.GetTriangles(source.subMeshCount));
                CollectionAssert.AreEqual(source.vertices, skin.sharedMesh.vertices);
                CollectionAssert.AreEqual(source.boneWeights, skin.sharedMesh.boneWeights);
                CollectionAssert.AreEqual(source.bindposes, skin.sharedMesh.bindposes);
                CollectionAssert.AreEqual(bones, skin.bones);
                Assert.AreEqual(bounds, skin.localBounds);
                for (int i = 0; i < materials.Length; i++)
                {
                    CollectionAssert.AreEqual(source.GetTriangles(i), skin.sharedMesh.GetTriangles(i));
                    var material = skin.sharedMaterials[i];
                    Assert.AreEqual(AssetDatabase.GetAssetPath(materials[i]), AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual(ModelLibrary.SpiritShader, material.shader.name);
                    Assert.AreEqual(1f, material.GetFloat("_TiffanyStencil"));
                    Assert.IsFalse(material.IsKeywordEnabled("_TIFFANY_CHARACTER"));
                    var entry = System.Array.Find(spec.materials, item => item.name == material.name);
                    ColorUtility.TryParseHtmlString(entry.rimColor, out var color);
                    for (int c = 0; c < 4; c++) Assert.AreEqual(color[c], material.GetColor("_RimColor")[c], 1e-4f);
                    Assert.AreEqual(entry.rimPower, material.GetFloat("_RimPower"));
                    Assert.AreEqual(entry.rimIntensity, material.GetFloat("_RimIntensity"));
                }
                Assert.AreEqual(GeneratedMaterials.SilhouetteShader, skin.sharedMaterials[materials.Length].shader.name);
                AssetDatabase.SaveAssets();
                string path = AssetDatabase.GetAssetPath(skin.sharedMesh);
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(skin.sharedMesh, out string guid, out long id));
                var savedMesh = Object.Instantiate(skin.sharedMesh);
                var snapshots = System.Array.ConvertAll(skin.sharedMaterials, material => new MaterialAssetSnapshot(material));
                try
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<Mesh>(path));
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        foreach (string materialPath in materialFiles.Keys)
                        {
                            Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<Material>(materialPath));
                            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
                        }
                        var rebuilt = ModelLibrary.Instantiate(model, root.transform);
                        CharacterSilhouetteBuilder.Add(rebuilt.transform);
                        AssetDatabase.SaveAssets();
                        foreach (var file in materialFiles) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
                        var rebuiltSkin = rebuilt.GetComponentInChildren<SkinnedMeshRenderer>();
                        Assert.AreEqual(bounds, rebuiltSkin.localBounds);
                        var mesh = rebuiltSkin.sharedMesh;
                        Assert.AreEqual(path, AssetDatabase.GetAssetPath(mesh));
                        Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string actualGuid, out long actualId));
                        Assert.AreEqual(guid, actualGuid);
                        Assert.AreEqual(id, actualId);
                        Assert.AreEqual(source.name + "Silhouette", mesh.name);
                        AssertMesh(savedMesh, mesh);
                        for (int i = 0; i < snapshots.Length; i++) snapshots[i].AssertUnchanged(rebuiltSkin.sharedMaterials[i]);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(savedMesh);
                    foreach (var snapshot in snapshots) snapshot.Dispose();
                }
            }
            finally
            {
                ModelLibrary.materialFolder = previous;
            }
        }

        static void AssertMesh(Mesh expected, Mesh actual)
        {
            Assert.AreEqual(expected.subMeshCount, actual.subMeshCount);
            Assert.AreEqual(expected.bounds, actual.bounds);
            Assert.AreEqual(expected.indexFormat, actual.indexFormat);
            CollectionAssert.AreEqual(expected.vertices, actual.vertices);
            CollectionAssert.AreEqual(expected.normals, actual.normals);
            CollectionAssert.AreEqual(expected.tangents, actual.tangents);
            CollectionAssert.AreEqual(expected.colors, actual.colors);
            Assert.AreEqual(expected.blendShapeCount, actual.blendShapeCount);
            CollectionAssert.AreEqual(expected.boneWeights, actual.boneWeights);
            CollectionAssert.AreEqual(expected.bindposes, actual.bindposes);
            for (int i = 0; i < expected.subMeshCount; i++)
            {
                Assert.AreEqual(expected.GetTopology(i), actual.GetTopology(i));
                CollectionAssert.AreEqual(expected.GetIndices(i), actual.GetIndices(i));
            }
            var expectedUVs = new List<Vector4>();
            var actualUVs = new List<Vector4>();
            for (int i = 0; i < 8; i++)
            {
                expected.GetUVs(i, expectedUVs);
                actual.GetUVs(i, actualUVs);
                CollectionAssert.AreEqual(expectedUVs, actualUVs);
            }
        }

        [Test]
        public void WitchCannotReceiveASilhouette()
        {
            root.AddComponent<WitchController>();
            Assert.Throws<System.InvalidOperationException>(() => CharacterSilhouetteBuilder.Add(root.transform));
            Assert.IsEmpty(root.GetComponentsInChildren<Renderer>(true));
        }
    }
}
