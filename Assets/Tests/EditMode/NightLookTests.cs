using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class NightLookTests
    {
        [Test]
        public void PreviewPreservesMaterialAndPerLayerProperties()
        {
            var previousSun = RenderSettings.sun;
            var ambient = RenderSettings.ambientLight;
            var color = RenderSettings.fogColor;
            bool enabled = RenderSettings.fog;
            var mode = RenderSettings.fogMode;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            var root = new GameObject("NightPreview Test");
            var cameraObject = new GameObject("NightPreview Camera");
            var follow = cameraObject.AddComponent<FollowCamera>();
            var camera = cameraObject.GetComponent<Camera>();
            follow.target = root.transform;
            var moon = root.AddComponent<Light>();
            moon.type = LightType.Directional;
            var fog = root.AddComponent<GroundFog>();
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Generated/Materials/GroundFog.mat");
            using var snapshot = new MaterialAssetSnapshot(renderer.sharedMaterial);
            fog.layers = new[] { new GroundFog.Layer { renderer = renderer, density = 0.7f, pathCorner = Vector3.one } };
            fog.ApplyProperties();
            RenderSettings.sun = moon;
            var properties = new MaterialPropertyBlock();
            try
            {
                foreach (var view in NightView.Combinations)
                {
                    var look = view.Look;
                    NightPreview.ApplyView(view, follow, fog, properties);
                    renderer.GetPropertyBlock(properties);
                    Assert.AreEqual(view.CameraDistance, follow.distance);
                    Assert.AreEqual(0f, follow.lookAhead);
                    Assert.Less(Vector3.Distance(camera.transform.position + camera.transform.forward * follow.distance,
                        root.transform.position + Vector3.up * follow.focusHeight), 0.001f);
                    Assert.IsTrue(RenderSettings.fog);
                    Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
                    Assert.AreEqual(view.FogStartDistance, RenderSettings.fogStartDistance);
                    Assert.AreEqual(view.FogEndDistance, RenderSettings.fogEndDistance);
                    Assert.AreEqual(view.FarClipPlane, camera.farClipPlane);
                    Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
                    Assert.AreEqual(look.FogColor, camera.backgroundColor);
                    Assert.AreEqual(look.MoonIntensity, moon.intensity);
                    Assert.AreEqual(look.AmbientColor, RenderSettings.ambientLight);
                    Assert.AreEqual(look.FogColor, RenderSettings.fogColor);
                    Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? look.GroundFogColor.linear : look.GroundFogColor;
                    Vector4 actual = properties.GetVector("_BaseColor");
                    for (int channel = 0; channel < 4; channel++) Assert.AreEqual(expected[channel], actual[channel], 0.00001f);
                    Assert.AreEqual(0.7f, properties.GetFloat("_Density"));
                    Assert.AreEqual((Vector4)Vector3.one, properties.GetVector("_PathCorner"));
                    Assert.AreEqual(look.GroundFogLightStrength, properties.GetFloat("_LightStrength"));
                    snapshot.AssertUnchanged(renderer.sharedMaterial);
                }
            }
            finally
            {
                RenderSettings.sun = previousSun;
                RenderSettings.ambientLight = ambient;
                RenderSettings.fogColor = color;
                RenderSettings.fog = enabled;
                RenderSettings.fogMode = mode;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [TestCase(0, "Hoy", 36f, "Current", 40f, 76f, 80f)]
        [TestCase(1, "D28B", 28f, "B", 32f, 52f, 56f)]
        [TestCase(2, "D24B", 24f, "B", 28f, 48f, 52f)]
        [TestCase(3, "D24C", 24f, "C", 28f, 48f, 52f)]
        public void PreviewCombinationsMatchCameraNightFogAndFar(int index, string name, float distance,
            string lookName, float start, float end, float far)
        {
            Assert.AreEqual(4, NightView.Combinations.Count);
            var view = NightView.Combinations[index];
            Assert.AreEqual(name, view.Name);
            Assert.AreEqual(distance, view.CameraDistance);
            Assert.AreEqual(lookName, view.Look.Name);
            Assert.AreEqual(start, view.FogStartDistance);
            Assert.AreEqual(end, view.FogEndDistance);
            Assert.AreEqual(far, view.FarClipPlane);
            Assert.Greater(view.FogStartDistance, view.CameraDistance);
            Assert.AreEqual(view.FogEndDistance + 4f, view.FarClipPlane);
        }

        [Test]
        public void CurrentExactlyPreservesTheOriginalNightAndRemainsDefault()
        {
            var look = NightLook.Current;
            Assert.AreSame(look, NightLook.Default);
            Assert.AreEqual(0.32f, look.MoonIntensity);
            Assert.AreEqual(new Color(0.025f, 0.035f, 0.065f), look.AmbientColor);
            Assert.AreEqual(new Color(0.018f, 0.028f, 0.052f), look.FogColor);
            Assert.AreEqual(new Color(0.035f, 0.045f, 0.065f, 0.5f).gamma, look.GroundFogColor);
            Assert.AreEqual(0.35f, look.GroundFogLightStrength);
            Assert.AreEqual(40f, NightLook.FogStartDistance);
            Assert.AreEqual(76f, NightLook.FogEndDistance);
        }

        [TestCase(1, "A", 0.18f, 0.5f)]
        [TestCase(2, "B", 0.10f, 0.3f)]
        [TestCase(3, "C", 0.05f, 0.15f)]
        public void PresetsDarkenMoonAmbientAndFogWithoutWeakeningLightHalos(int index, string name, float moon, float scale)
        {
            var look = NightLook.Presets[index];
            var previous = NightLook.Presets[index - 1];
            Assert.AreEqual(name, look.Name);
            Assert.AreEqual(moon, look.MoonIntensity);
            Assert.Less(look.MoonIntensity, previous.MoonIntensity);
            for (int channel = 0; channel < 3; channel++)
            {
                Assert.AreEqual(NightLook.Current.AmbientColor[channel] * scale, look.AmbientColor[channel]);
                Assert.Less(look.AmbientColor[channel], previous.AmbientColor[channel]);
                Assert.AreEqual(NightLook.Current.FogColor[channel] * scale, look.FogColor[channel]);
                Assert.Less(look.FogColor[channel], previous.FogColor[channel]);
                Assert.AreEqual(NightLook.Current.GroundFogColor.linear[channel] * scale,
                    look.GroundFogColor.linear[channel], 0.00001f);
                Assert.Less(look.GroundFogColor[channel], previous.GroundFogColor[channel]);
            }
            Assert.AreEqual(NightLook.Current.GroundFogColor.a, look.GroundFogColor.a);
            Assert.AreEqual(NightLook.Current.GroundFogLightStrength, look.GroundFogLightStrength);
        }
    }
}
