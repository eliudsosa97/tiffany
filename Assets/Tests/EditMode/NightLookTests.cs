using System.Reflection;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed class NightLookTests
    {
        [Test]
        public void BuiltNightUsesGameLightAndFog()
        {
            var previousSun = RenderSettings.sun;
            var skybox = RenderSettings.skybox;
            var ambientMode = RenderSettings.ambientMode;
            var ambient = RenderSettings.ambientLight;
            float reflection = RenderSettings.reflectionIntensity;
            var color = RenderSettings.fogColor;
            bool enabled = RenderSettings.fog;
            var mode = RenderSettings.fogMode;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            Light moon = null;
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                typeof(SceneBuilder).GetMethod("ConfigureNight", flags).Invoke(null, null);
                typeof(SceneBuilder).GetMethod("CreateMoon", flags).Invoke(null, null);
                moon = RenderSettings.sun;
                Assert.IsNotNull(moon);
                Assert.AreEqual(LightType.Directional, moon.type);
                Assert.AreEqual(NightLook.Default.MoonIntensity, moon.intensity);
                Assert.IsNull(RenderSettings.skybox);
                Assert.AreEqual(AmbientMode.Flat, RenderSettings.ambientMode);
                Assert.AreEqual(NightLook.Default.AmbientColor, RenderSettings.ambientLight);
                Assert.AreEqual(0f, RenderSettings.reflectionIntensity);
                Assert.IsTrue(RenderSettings.fog);
                Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
                Assert.AreEqual(NightLook.Default.FogColor, RenderSettings.fogColor);
                Assert.AreEqual(NightView.FogStartDistance, RenderSettings.fogStartDistance);
                Assert.AreEqual(NightView.FogEndDistance, RenderSettings.fogEndDistance);
            }
            finally
            {
                if (moon != null) Object.DestroyImmediate(moon.gameObject);
                RenderSettings.sun = previousSun;
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                RenderSettings.reflectionIntensity = reflection;
                RenderSettings.fogColor = color;
                RenderSettings.fog = enabled;
                RenderSettings.fogMode = mode;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
            }
        }

        [Test]
        public void GameViewKeepsFogAndFarTiedToCameraDistance()
        {
            Assert.AreEqual(24f, NightView.CameraDistance);
            Assert.AreEqual(NightView.CameraDistance + 4f, NightView.FogStartDistance);
            Assert.AreEqual(NightView.CameraDistance + 24f, NightView.FogEndDistance);
            Assert.AreEqual(NightView.FogEndDistance + 4f, NightView.FarClipPlane);
        }

        [Test]
        public void GameNightDarkensMoonAmbientAndFogWithoutWeakeningLightHalos()
        {
            var look = NightLook.Default;
            var previousAmbient = new Color(0.025f, 0.035f, 0.065f);
            var previousFog = new Color(0.018f, 0.028f, 0.052f);
            var previousGroundFog = new Color(0.035f, 0.045f, 0.065f, 0.5f);
            Assert.AreEqual(0.10f, look.MoonIntensity);
            Assert.AreEqual(0.3f, look.AmbientScale);
            Assert.Less(look.MoonIntensity, 0.32f);
            for (int channel = 0; channel < 3; channel++)
            {
                Assert.AreEqual(previousAmbient[channel] * 0.3f, look.AmbientColor[channel]);
                Assert.Less(look.AmbientColor[channel], previousAmbient[channel]);
                Assert.AreEqual(previousFog[channel] * 0.3f, look.FogColor[channel]);
                Assert.Less(look.FogColor[channel], previousFog[channel]);
                Assert.AreEqual(previousGroundFog[channel] * 0.3f,
                    look.GroundFogColor.linear[channel], 0.00001f);
                Assert.Less(look.GroundFogColor[channel], previousGroundFog.gamma[channel]);
            }
            Assert.AreEqual(previousGroundFog.a, look.GroundFogColor.a);
            Assert.AreEqual(0.35f, look.GroundFogLightStrength);
        }
    }
}
