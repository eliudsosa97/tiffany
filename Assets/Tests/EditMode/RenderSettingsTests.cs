using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed class RenderSettingsTests
    {
        [Test]
        public void WebQualityRendersAtFullScale()
        {
            using var qualitySettings = new SerializedObject(QualitySettings.GetQualitySettings());
            var defaults = qualitySettings.FindProperty("m_PerPlatformDefaultQuality");
            Assert.IsNotNull(defaults, "No se encontraron las calidades predeterminadas por plataforma.");

            int webQuality = -1;
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue != "WebGL")
                    continue;

                webQuality = entry.FindPropertyRelative("second").intValue;
                break;
            }

            Assert.GreaterOrEqual(webQuality, 0, "No se encontró la calidad predeterminada de WebGL.");
            RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(webQuality);
            Assert.IsNotNull(asset, "La calidad predeterminada de WebGL debe tener un asset de renderizado.");

            using var renderSettings = new SerializedObject(asset);
            var renderScale = renderSettings.FindProperty("m_RenderScale");
            Assert.IsNotNull(renderScale, "El asset de renderizado de WebGL debe definir m_RenderScale.");
            Assert.AreEqual(1f, renderScale.floatValue, 1e-4f,
                "Una escala de renderizado menor que 1 deja la build web sin dibujar la escena.");
        }
    }
}
