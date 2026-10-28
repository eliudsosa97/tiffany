using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class WitchBuilderTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            WitchBuilder.materialFolder = "Assets/WitchBuilderTestTemp/Materials";
            root = new GameObject("WitchBuilderTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset("Assets/WitchBuilderTestTemp");
            WitchBuilder.materialFolder = WitchBuilder.MaterialFolder;
        }

        [Test]
        public void FigureIsTallThinAndAsymmetric()
        {
            var figure = WitchBuilder.BuildFigure(root.transform);
            var bounds = CombinedBounds(figure.transform);
            Assert.That(bounds.size.y, Is.InRange(2.25f, 2.4f));
            Assert.AreEqual(0f, bounds.min.y, 1e-4f);
            Assert.Less(figure.transform.Find("Torso").GetComponent<Renderer>().bounds.size.x, 0.35f);
            var shoulderL = figure.transform.Find("ShoulderL");
            var shoulderR = figure.transform.Find("ShoulderR");
            Assert.Greater(shoulderR.position.y - shoulderL.position.y, 0.1f);
            Assert.Less(CombinedBounds(shoulderL.Find("ArmL")).min.y, 0.3f);
            Assert.Greater(CombinedBounds(shoulderR.Find("ArmR")).min.y, 0.9f);
            var head = figure.transform.Find("Head");
            var neck = figure.transform.Find("Neck");
            Assert.Less(head.localPosition.x, neck.localPosition.x);
            Assert.Less(head.up.x, 0f);
            Assert.Less(neck.up.x, 0f);
        }

        [Test]
        public void FigureHasHairAndClothInFrontOfTheFaceAndChest()
        {
            var figure = WitchBuilder.BuildFigure(root.transform);
            var torso = figure.transform.Find("Torso").GetComponent<Renderer>();
            var head = figure.transform.Find("Head").GetComponent<Renderer>();
            var hair = figure.transform.Find("HairFront").GetComponent<Renderer>();
            var shawl = figure.transform.Find("ShawlFront").GetComponent<Renderer>();

            Assert.AreEqual("WitchCloth", torso.sharedMaterial.name);
            Assert.AreSame(torso.sharedMaterial, shawl.sharedMaterial);
            Assert.AreEqual("WitchHair", hair.sharedMaterial.name);
            Assert.Less(hair.sharedMaterial.GetColor("_BaseColor").maxColorComponent, 0.05f);
            Assert.Greater(hair.bounds.center.z, head.bounds.max.z);
            Assert.Greater(hair.bounds.max.y, head.bounds.center.y);
            Assert.Less(hair.bounds.min.y, torso.bounds.max.y);
            Assert.Greater(shawl.bounds.center.z, torso.bounds.max.z);
            Assert.Greater(shawl.bounds.max.y, figure.transform.Find("ShoulderR").position.y);
            Assert.Less(shawl.bounds.min.y, torso.bounds.max.y);
        }

        [Test]
        public void FigureHasNoCollidersAndUsesSavedMaterials()
        {
            root.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            root.transform.localScale = new Vector3(2f, 3f, 4f);
            var figure = WitchBuilder.BuildFigure(root.transform);
            Assert.AreEqual("WitchFigure", figure.name);
            Assert.AreSame(root.transform, figure.transform.parent);
            Assert.AreEqual(Vector3.zero, figure.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, figure.transform.localRotation);
            Assert.AreEqual(Vector3.one, figure.transform.localScale);
            Assert.IsEmpty(figure.GetComponentsInChildren<Collider>());
            var renderers = figure.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
            {
                Assert.IsTrue(AssetDatabase.Contains(renderer.sharedMaterial), renderer.name);
                StringAssert.StartsWith(WitchBuilder.materialFolder + "/", AssetDatabase.GetAssetPath(renderer.sharedMaterial));
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, renderer.name);
                Assert.IsTrue(renderer.receiveShadows, renderer.name);
                Assert.AreEqual("Universal Render Pipeline/Lit", renderer.sharedMaterial.shader.name);
                Assert.AreEqual(0.1f, renderer.sharedMaterial.GetFloat("_Smoothness"), 1e-4f);
            }
        }

        [Test]
        public void FireBallHasOnePointLightWithoutShadows()
        {
            var light = WitchBuilder.BuildFireBall(root.transform, out var ball);
            Assert.AreEqual("WitchFireBall", ball.name);
            Assert.AreSame(root.transform, ball.transform.parent);
            Assert.AreEqual(Vector3.zero, ball.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, ball.transform.localRotation);
            var lights = ball.GetComponentsInChildren<Light>();
            Assert.AreEqual(1, lights.Length);
            Assert.AreSame(light, lights[0]);
            Assert.AreSame(ball.transform, light.transform.parent);
            Assert.AreEqual(Vector3.zero, light.transform.localPosition);
            Assert.AreEqual(LightType.Point, light.type);
            Assert.GreaterOrEqual(light.range, 25f);
            Assert.AreEqual(30f, light.range);
            Assert.AreEqual(150f, light.intensity);
            Assert.AreEqual(new Color(1f, 0.55f, 0.2f), light.color);
            Assert.AreEqual(LightShadows.None, light.shadows);
            Assert.AreEqual(LightRenderMode.ForcePixel, light.renderMode);
            Assert.IsEmpty(ball.GetComponentsInChildren<Collider>());
            var renderers = ball.GetComponentsInChildren<Renderer>();
            Assert.AreEqual(1, renderers.Length);
            var renderer = renderers[0];
            Assert.AreEqual(0.9f, renderer.bounds.size.x, 1e-4f);
            Assert.AreEqual(0.9f, renderer.bounds.size.y, 1e-4f);
            Assert.AreEqual(0.9f, renderer.bounds.size.z, 1e-4f);
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
            Assert.IsFalse(renderer.receiveShadows);
            Assert.IsTrue(AssetDatabase.Contains(renderer.sharedMaterial));
            StringAssert.StartsWith(WitchBuilder.materialFolder + "/", AssetDatabase.GetAssetPath(renderer.sharedMaterial));
            Assert.AreEqual("Universal Render Pipeline/Unlit", renderer.sharedMaterial.shader.name);
        }

        [Test]
        public void MaterialsKeepTheirGuidWhenRebuilt()
        {
            WitchBuilder.BuildFigure(root.transform);
            string path = $"{WitchBuilder.materialFolder}/WitchSkin.mat";
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.IsNotEmpty(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var color = material.GetColor("_BaseColor");
            material.SetColor("_BaseColor", Color.magenta);
            EditorUtility.SetDirty(material);

            WitchBuilder.BuildFigure(root.transform);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(color, AssetDatabase.LoadAssetAtPath<Material>(path).GetColor("_BaseColor"));
        }

        [Test]
        public void TestsLeaveNoMaterialsInTheSceneFolder()
        {
            string path = WitchBuilder.MaterialFolder + "/WitchSkin.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                Assert.Ignore("El material de la escena ya existía antes del test.");

            WitchBuilder.BuildFigure(root.transform);
            WitchBuilder.BuildFireBall(root.transform, out _);

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        static Bounds CombinedBounds(Transform parent)
        {
            var renderers = parent.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
