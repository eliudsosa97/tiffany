using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class EndingRulesTests
    {
        [TestCase(true, 0f, true, true)]
        [TestCase(true, 2.49f, true, true)]
        [TestCase(false, 1f, true, false)]
        [TestCase(true, 1f, false, false)]
        [TestCase(true, 2.5f, true, false)]
        [TestCase(true, 3f, true, false)]
        [TestCase(true, -1f, true, false)]
        [TestCase(true, float.NaN, true, false)]
        [TestCase(true, float.PositiveInfinity, true, false)]
        public void ManifestRequiresPowerRangeAndInteraction(bool power, float distance, bool interact, bool expected)
        {
            var ending = new EndingRules();
            Assert.AreEqual(expected, ending.TryManifest(power, distance, interact));
            Assert.AreEqual(expected ? EndingStage.Approach : EndingStage.Idle, ending.Stage);
            Assert.AreEqual(0f, ending.ApproachProgress);
            Assert.AreEqual(0f, ending.FadeAlpha);
        }

        [TestCase(0f, EndingStage.Approach, 0f, 0f)]
        [TestCase(1f, EndingStage.Approach, 0.5f, 0f)]
        [TestCase(2f, EndingStage.Fade, 1f, 0f)]
        [TestCase(2.75f, EndingStage.Fade, 1f, 0.5f)]
        [TestCase(3.5f, EndingStage.Text, 1f, 1f)]
        [TestCase(7.5f, EndingStage.Credits, 1f, 1f)]
        public void ApproachPrecedesFadeWithIndependentProgress(float seconds, EndingStage stage, float progress, float alpha)
        {
            var ending = new EndingRules();
            ending.TryManifest(true, 0f, true);
            ending.Tick(seconds);
            Assert.AreEqual(stage, ending.Stage);
            Assert.AreEqual(progress, ending.ApproachProgress);
            Assert.AreEqual(alpha, ending.FadeAlpha);
            Assert.IsFalse(ending.TryManifest(true, 0f, true));
        }

        [Test]
        public void EndingFadesForOneAndAHalfSecondsThenShowsStoryForFourSecondsAndKeepsCredits()
        {
            var ending = new EndingRules();
            ending.Tick(100f);
            Assert.AreEqual(EndingStage.Idle, ending.Stage);
            Assert.IsTrue(ending.TryManifest(true, 1f, true));
            ending.Tick(EndingRules.ApproachSeconds);
            ending.Tick(0.75f);
            Assert.AreEqual(EndingStage.Fade, ending.Stage);
            Assert.AreEqual(0.5f, ending.FadeAlpha);
            Assert.IsFalse(ending.TryManifest(true, 1f, true));
            ending.Tick(0.75f);
            Assert.AreEqual(EndingStage.Text, ending.Stage);
            Assert.AreEqual(1f, ending.FadeAlpha);
            Assert.AreEqual(1f, ending.ApproachProgress);
            ending.Tick(3.99f);
            Assert.AreEqual(EndingStage.Text, ending.Stage);
            ending.Tick(0.02f);
            Assert.AreEqual(EndingStage.Credits, ending.Stage);
            ending.Tick(100f);
            Assert.IsFalse(ending.TryManifest(true, 1f, true));
            Assert.AreEqual(EndingStage.Credits, ending.Stage);
            Assert.AreEqual(1f, ending.FadeAlpha);
        }

        [Test]
        public void LargeFramePreservesTimeAcrossStages()
        {
            var ending = new EndingRules();
            ending.TryManifest(true, 0f, true);
            ending.Tick(EndingRules.ApproachSeconds + EndingRules.FadeSeconds + EndingRules.TextSeconds);
            Assert.AreEqual(EndingStage.Credits, ending.Stage);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void InvalidTimeCannotAdvanceOrReverseEnding(float dt)
        {
            var ending = new EndingRules();
            ending.TryManifest(true, 0f, true);
            ending.Tick(EndingRules.ApproachSeconds + 0.75f);
            ending.Tick(dt);
            Assert.AreEqual(EndingStage.Fade, ending.Stage);
            Assert.AreEqual(0.5f, ending.FadeAlpha);
        }

        [Test]
        public void EndingCannotBeDismissedByPauseNotebookOrStartAndPreviewCanRestorePlay()
        {
            var flow = new InterfaceFlow(true);
            flow.PressStart();
            flow.SetEnding(true);
            flow.PressPause();
            Assert.AreEqual(InterfaceScreen.Ending, flow.Screen);
            flow.PressNotebook();
            Assert.AreEqual(InterfaceScreen.Ending, flow.Screen);
            flow.PressStart();
            Assert.AreEqual(InterfaceScreen.Ending, flow.Screen);
            Assert.IsTrue(flow.IsFrozen);
            flow.SetEnding(false);
            Assert.AreEqual(InterfaceScreen.Playing, flow.Screen);
            Assert.IsFalse(flow.IsFrozen);
        }
    }
}
