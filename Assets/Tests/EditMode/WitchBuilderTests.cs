using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class WitchBuilderTests
    {
        const string TempFolder = "Assets/WitchBuilderTestTemp";
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            GeneratedMaterials.folder = TempFolder + "/GeneratedMaterials";
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            root = new GameObject("WitchBuilderTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(TempFolder);
            GeneratedMaterials.folder = GeneratedMaterials.DefaultFolder;
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        [Test]
        public void BuiltWitchGroundMaskExcludesCoverAndKeepsDefaultRaycastLayers()
        {
            var followCamera = new GameObject("Camera").AddComponent<FollowCamera>();
            followCamera.transform.SetParent(root.transform, false);
            var controller = SceneBuilder.BuildWitch(root.transform, null, root.transform, new[] { Vector3.zero }, followCamera);
            int cover = LayerMask.GetMask(ForestLayout.CoverLayer);
            Assert.AreNotEqual(0, cover);
            Assert.AreEqual(0, controller.groundMask.value & cover);
            Assert.AreNotEqual(0, controller.groundMask.value & LayerMask.GetMask("Default"));
            Assert.AreEqual(Physics.DefaultRaycastLayers, controller.groundMask.value | cover);
        }

        [Test]
        public void FigureHasNeutralRootAndDirectPoseChildren()
        {
            root.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            root.transform.localScale = new Vector3(2f, 3f, 4f);
            var figure = WitchBuilder.BuildFigure(root.transform, out var stand, out var crouch);

            Assert.AreEqual("WitchFigure", figure.name);
            Assert.AreSame(root.transform, figure.transform.parent);
            Assert.AreEqual(Vector3.zero, figure.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, figure.transform.localRotation);
            Assert.AreEqual(Vector3.one, figure.transform.localScale);
            Assert.AreEqual("WitchStand", stand.name);
            Assert.AreEqual("WitchCrouch", crouch.name);
            Assert.AreSame(figure.transform, stand.transform.parent);
            Assert.AreSame(figure.transform, crouch.transform.parent);
            Assert.IsTrue(stand.activeSelf);
            Assert.IsFalse(crouch.activeSelf);
        }

        [Test]
        public void FigurePosesHaveExpectedHeights()
        {
            WitchBuilder.BuildFigure(root.transform, out var stand, out var crouch);

            Assert.That(CombinedBounds(stand.transform).size.y, Is.InRange(2.1f, 2.4f));
            Assert.That(CombinedBounds(crouch.transform).size.y, Is.InRange(1.3f, 1.6f));
        }

        [Test]
        public void FigureHasNoCollidersAndUsesModelMaterials()
        {
            var figure = WitchBuilder.BuildFigure(root.transform, out _, out _);

            Assert.IsEmpty(figure.GetComponentsInChildren<Collider>(true));
            var renderers = figure.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, renderer.name);
                Assert.IsTrue(renderer.receiveShadows, renderer.name);
                Assert.IsNotEmpty(renderer.sharedMaterials, renderer.name);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name);
                    Assert.IsTrue(AssetDatabase.Contains(material), renderer.name);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                }
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
            StringAssert.StartsWith(GeneratedMaterials.folder + "/", AssetDatabase.GetAssetPath(renderer.sharedMaterial));
            Assert.AreEqual("Universal Render Pipeline/Unlit", renderer.sharedMaterial.shader.name);
        }

        [Test]
        public void FireMaterialKeepsItsGuidWhenRebuilt()
        {
            WitchBuilder.BuildFireBall(root.transform, out _);
            string path = $"{GeneratedMaterials.folder}/WitchFire.mat";
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.IsNotEmpty(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var color = material.GetColor("_BaseColor");
            material.SetColor("_BaseColor", Color.magenta);
            EditorUtility.SetDirty(material);

            WitchBuilder.BuildFireBall(root.transform, out _);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(color, AssetDatabase.LoadAssetAtPath<Material>(path).GetColor("_BaseColor"));
        }

        [Test]
        public void TestsLeaveNoMaterialsInTheSceneFolder()
        {
            string path = GeneratedMaterials.DefaultFolder + "/WitchFire.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                Assert.Ignore("El material de la escena ya existía antes del test.");

            WitchBuilder.BuildFireBall(root.transform, out _);

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        static Bounds CombinedBounds(Transform parent)
        {
            var renderers = parent.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
