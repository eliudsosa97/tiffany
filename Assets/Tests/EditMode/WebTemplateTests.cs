using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WebTemplateTests
    {
        const string ProjectPrefix = "PROJECT:";

        [Test]
        public void WebBuildTemplateExistsInProject()
        {
            StringAssert.StartsWith(ProjectPrefix, BuildTools.WebTemplate);
            string templatePath = TemplatePath();

            Assert.IsTrue(Directory.Exists(templatePath));
            Assert.IsTrue(File.Exists(Path.Combine(templatePath, "index.html")));
        }

        [Test]
        public void TemplateLimitsPixelRatioWithoutBranding()
        {
            string html = File.ReadAllText(Path.Combine(TemplatePath(), "index.html"));

            StringAssert.Contains("devicePixelRatio", html);
            StringAssert.DoesNotContain("unity-logo", html);
            StringAssert.DoesNotContain("unity-footer", html);
        }

        [Test]
        public void TemplateDeclaresAnIconSoTheBrowserDoesNotRequestFavicon()
        {
            string html = File.ReadAllText(Path.Combine(TemplatePath(), "index.html"));

            StringAssert.Contains("<link rel=\"icon\" href=\"data:,\">", html);
        }

        static string TemplatePath()
        {
            string name = BuildTools.WebTemplate.Substring(ProjectPrefix.Length);
            return Path.Combine(Application.dataPath, "WebGLTemplates", name);
        }
    }
}
