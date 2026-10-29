using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class FrameSettingsTests
    {
        [TestCase(false, 60)]
        [TestCase(true, -1)]
        public void TargetFrameRateDependsOnPlatform(bool webGL, int expected)
        {
            Assert.That(FrameSettings.TargetFrameRate(webGL), Is.EqualTo(expected));
        }

        [TestCase("-autocapture")]
        [TestCase("-capturevideo")]
        [TestCase("-playthrough")]
        [TestCase("-nightpreview")]
        public void AutomationRunsInBackground(string argument)
        {
            Assert.That(FrameSettings.RunInBackground(new[] { argument }), Is.True);
            Assert.That(FrameSettings.RunInBackground(new[] { "Tiffany", "-screen-width", "1600", argument, "output" }), Is.True);
        }

        [Test]
        public void CombinedAutomationRunsInBackground()
        {
            Assert.That(FrameSettings.RunInBackground(new[] { "-autocapture", "-capturevideo", "-playthrough", "-nightpreview" }), Is.True);
        }

        [Test]
        public void RunsInForegroundWithoutAutomation()
        {
            Assert.That(FrameSettings.RunInBackground(null), Is.False);
            Assert.That(FrameSettings.RunInBackground(System.Array.Empty<string>()), Is.False);
            Assert.That(FrameSettings.RunInBackground(new[] { "Tiffany", "-screen-width", "1600" }), Is.False);
            Assert.That(FrameSettings.RunInBackground(new[] { "-autocapturex" }), Is.False);
            Assert.That(FrameSettings.RunInBackground(new[] { "-nightpreviewx" }), Is.False);
        }
    }
}
