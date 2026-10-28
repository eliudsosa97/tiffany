using System;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class GeneratedMaterialsTests
    {
        [SetUp]
        public void SetUp()
        {
            GeneratedMaterials.folder = "Assets/GeneratedMaterialsTestTemp/Materials";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset("Assets/GeneratedMaterialsTestTemp");
            GeneratedMaterials.folder = GeneratedMaterials.DefaultFolder;
        }

        [TestCase("dry_ground_rocks")]
        [TestCase("rock_boulder_dry")]
        [TestCase("gravel_ground_01")]
        [TestCase("dry_decay_leaves")]
        [TestCase("burned_ground_01")]
        public void GetTexturedCreatesSavedMaterialWithMapsAndTiling(string textureName)
        {
            var tint = new Color(0.46f, 0.30f, 0.18f);
            var material = GeneratedMaterials.GetTextured("Ground", tint, textureName, 3.5f);
            string textureFolder = $"Assets/Art/CC0/PolyHaven/{textureName}";
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/{textureName}_diff.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/{textureName}_nor_gl.jpg");

            Assert.IsTrue(AssetDatabase.Contains(material));
            Assert.AreEqual(GeneratedMaterials.folder + "/Ground.mat", AssetDatabase.GetAssetPath(material));
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsNotNull(diffuse);
            Assert.IsNotNull(normal);
            Assert.AreSame(diffuse, material.GetTexture("_BaseMap"));
            Assert.AreSame(diffuse, material.mainTexture);
            Assert.AreSame(normal, material.GetTexture("_BumpMap"));
            Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
            Assert.AreEqual(1f, material.GetFloat("_BumpScale"));
            AssertColor(tint, material.GetColor("_BaseColor"));
            Assert.AreEqual(0.08f, material.GetFloat("_Smoothness"), 1e-4f);
            Assert.AreEqual(0f, material.GetFloat("_Metallic"));
            Assert.AreEqual(new Vector2(3.5f, 3.5f), material.mainTextureScale);
            Assert.AreEqual(new Vector2(3.5f, 3.5f), material.GetTextureScale("_BaseMap"));
            Assert.AreEqual(new Vector2(3.5f, 3.5f), material.GetTextureScale("_BumpMap"));
        }

        [Test]
        public void GetTexturedUpdatesExistingMaterialWithoutChangingGuid()
        {
            var original = GeneratedMaterials.Get("Ground", Color.magenta, unlit: true);
            string path = AssetDatabase.GetAssetPath(original);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.IsNotEmpty(guid);

            var material = GeneratedMaterials.GetTextured("Ground", Color.green, "dry_ground_rocks", 20f, 0.25f);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            AssertColor(Color.green, material.GetColor("_BaseColor"));
            Assert.IsNotNull(material.GetTexture("_BaseMap"));
            Assert.IsNotNull(material.GetTexture("_BumpMap"));
            Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
            Assert.AreEqual(0.25f, material.GetFloat("_Smoothness"), 1e-4f);
            Assert.AreEqual(new Vector2(20f, 20f), material.mainTextureScale);
            Assert.AreEqual(new Vector2(20f, 20f), material.GetTextureScale("_BumpMap"));
        }

        [Test]
        public void InstancingIsOptInAndSurvivesCallsWithoutTheOption()
        {
            Assert.IsFalse(GeneratedMaterials.Get("Ordinary", Color.white).enableInstancing);
            var instanced = GeneratedMaterials.Get("Instanced", Color.white, instancing: true);
            Assert.IsTrue(instanced.enableInstancing);
            Assert.IsTrue(GeneratedMaterials.Get("Instanced", Color.white).enableInstancing);
            Assert.IsTrue(GeneratedMaterials.GetTextured("Leaves", Color.white, "dry_decay_leaves", 1f,
                instancing: true).enableInstancing);
            Assert.IsFalse(GeneratedMaterials.Get("Instanced", Color.white, instancing: false).enableInstancing);
        }

        [TestCase(GroundType.Dirt)]
        [TestCase(GroundType.Grass)]
        [TestCase(GroundType.Slab)]
        [TestCase(GroundType.Gravel)]
        [TestCase(GroundType.Leaves)]
        [TestCase(GroundType.Branches)]
        public void GroundMaterialUsesErrorFreeForestGroundShader(GroundType type)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/ForestGround.shader");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));

            var material = GroundMaterials.For(type);

            Assert.AreSame(shader, material.shader);
            Assert.AreEqual($"{GeneratedMaterials.folder}/Route{type}.mat", AssetDatabase.GetAssetPath(material));
        }

        [Test]
        public void OccluderKeepsSavedMapsTilingAndInstancingWhenRegenerated()
        {
            var material = GeneratedMaterials.GetTextured("Tree", Color.green, "pine_bark", 2f, instancing: true);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material));
            var diffuse = material.GetTexture("_BaseMap");
            var normal = material.GetTexture("_BumpMap");
            var regenerated = GeneratedMaterials.GetTextured("Tree", Color.green, "pine_bark", 2f,
                instancing: true, occluder: true);
            Assert.AreSame(material, regenerated);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(regenerated)));
            Assert.AreEqual(GeneratedMaterials.OcclusionShader, material.shader.name);
            Assert.AreSame(diffuse, material.GetTexture("_BaseMap"));
            Assert.AreSame(normal, material.GetTexture("_BumpMap"));
            Assert.AreEqual(Vector2.one * 2f, material.mainTextureScale);
            Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsFalse(material.IsKeywordEnabled("_TIFFANY_CHARACTER"));
            Assert.IsTrue(material.enableInstancing);
            foreach (string pass in new[] { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster" })
                Assert.GreaterOrEqual(material.FindPass(pass), 0, pass);
        }

        [Test]
        public void GetTexturedThrowsWhenTextureDoesNotExist()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                GeneratedMaterials.GetTextured("Ground", Color.white, "missing_ground_texture", 3.5f));

            StringAssert.StartsWith(GameInfo.LogPrefix, exception.Message);
            StringAssert.Contains("Assets/Art/CC0/PolyHaven/missing_ground_texture/missing_ground_texture_diff.jpg", exception.Message);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(GeneratedMaterials.folder + "/Ground.mat"));
        }

        static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-4f);
            Assert.AreEqual(expected.g, actual.g, 1e-4f);
            Assert.AreEqual(expected.b, actual.b, 1e-4f);
            Assert.AreEqual(expected.a, actual.a, 1e-4f);
        }
    }
}
