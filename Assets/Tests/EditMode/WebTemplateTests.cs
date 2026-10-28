using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WebTemplateTests
    {
        [Test]
        public void ProjectTemplateContainsIndex()
        {
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates", "Tiffany");

            Assert.IsTrue(Directory.Exists(templatePath));
            Assert.IsTrue(File.Exists(Path.Combine(templatePath, "index.html")));
        }

        [Test]
        public void WebBuildUsesProjectTemplate()
        {
            Assert.AreEqual("PROJECT:Tiffany", BuildTools.WebTemplate);
        }

        [Test]
        public void TemplateLimitsPixelRatioWithoutBranding()
        {
            string indexPath = Path.Combine(Application.dataPath, "WebGLTemplates", "Tiffany", "index.html");
            string html = File.ReadAllText(indexPath);

            StringAssert.Contains("devicePixelRatio", html);
            StringAssert.DoesNotContain("unity-logo", html);
            StringAssert.DoesNotContain("unity-footer", html);
        }
    }
}
