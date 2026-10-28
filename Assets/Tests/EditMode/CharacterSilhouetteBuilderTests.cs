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
            renderer.sharedMaterial = material;
            body.SetActive(false);
            Assert.AreEqual(1, CharacterSilhouetteBuilder.Add(root.transform));
            CharacterSilhouetteBuilder.Add(root.transform);

            Assert.AreEqual(1, root.GetComponentsInChildren<Renderer>(true).Length);
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            Assert.AreSame(material, renderer.sharedMaterials[0]);
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
        public void WitchCannotReceiveASilhouette()
        {
            root.AddComponent<WitchController>();
            Assert.Throws<System.InvalidOperationException>(() => CharacterSilhouetteBuilder.Add(root.transform));
            Assert.IsEmpty(root.GetComponentsInChildren<Renderer>(true));
        }
    }
}
