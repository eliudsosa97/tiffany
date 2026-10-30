using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class DisplayRulesTests
    {
        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void StartupDependsOnPlatform(bool isEditor, bool isWebGL, bool expected)
        {
            Assert.AreEqual(expected, DisplayRules.StartsFullscreen(null, isEditor, isWebGL));
            Assert.AreEqual(expected, DisplayRules.StartsFullscreen(System.Array.Empty<string>(), isEditor, isWebGL));
            Assert.AreEqual(expected, DisplayRules.StartsFullscreen(new[] { "Tiffany", "-screen-width", "1600", "-autocapturex" }, isEditor, isWebGL));
        }

        [TestCase("-autocapture")]
        [TestCase("-capturevideo")]
        [TestCase("-playthrough")]
        [TestCase("-motiondemo")]
        public void AutomationPreservesWindowMode(string argument) =>
            Assert.IsFalse(DisplayRules.StartsFullscreen(new[] { "Tiffany", argument, "output" }, false, false));
    }
}
