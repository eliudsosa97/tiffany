using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class StorySequenceTests
    {
        [Test]
        public void EachLineFadesInHoldsAndFadesOutBeforeTheNext()
        {
            var story = new StorySequence();
            for (int index = 0; index < StorySequence.LineCount; index++)
            {
                Assert.That(story.Index, Is.EqualTo(index));
                Assert.That(story.IsFinished, Is.False);
                Assert.That(story.Alpha, Is.Zero);
                story.Tick(StorySequence.FadeSeconds / 2f);
                Assert.That(story.Alpha, Is.EqualTo(0.5f).Within(0.001f));
                story.Tick(StorySequence.FadeSeconds / 2f);
                Assert.That(story.Alpha, Is.EqualTo(1f));
                story.Tick(StorySequence.LineSeconds - 1.5f * StorySequence.FadeSeconds);
                Assert.That(story.Alpha, Is.EqualTo(0.5f).Within(0.001f));
                story.Tick(StorySequence.FadeSeconds / 2f);
            }
            Assert.That(story.IsFinished, Is.True);
            Assert.That(story.Alpha, Is.Zero);
        }

        [Test]
        public void PressSkipsOnlyCurrentLineAndLastPressFinishes()
        {
            var story = new StorySequence();
            story.Tick(2f);
            story.Advance();
            Assert.That(story.Index, Is.EqualTo(1));
            Assert.That(story.Alpha, Is.Zero);
            story.Tick(StorySequence.LineSeconds - 1f);
            Assert.That(story.Index, Is.EqualTo(1));
            story.Advance();
            Assert.That(story.Index, Is.EqualTo(2));
            Assert.That(story.IsFinished, Is.False);
            story.Advance();
            story.Advance();
            story.Tick(100f);
            Assert.That(story.Index, Is.EqualTo(StorySequence.LineCount));
            Assert.That(story.IsFinished, Is.True);
            Assert.That(story.Alpha, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        public void NonPositiveTimeDoesNotAdvance(float dt)
        {
            var story = new StorySequence();
            story.Tick(dt);
            Assert.That(story.Index, Is.Zero);
            Assert.That(story.Alpha, Is.Zero);
        }

        [Test]
        public void LargeTickCarriesTimeAcrossLinesAndFinishesAutomatically()
        {
            var story = new StorySequence();
            story.Tick(StorySequence.LineSeconds + StorySequence.FadeSeconds);
            Assert.That(story.Index, Is.EqualTo(1));
            Assert.That(story.Alpha, Is.EqualTo(1f).Within(0.001f));
            story.Tick(100f);
            Assert.That(story.IsFinished, Is.True);
        }

        [TestCase("-playthrough")]
        [TestCase("-autocapture")]
        [TestCase("-runTests")]
        [TestCase("-echodemo")]
        [TestCase("-capturevideo")]
        [TestCase("-motiondemo")]
        [TestCase("-witchdemo")]
        [TestCase("-fogdemo")]
        public void AutomatedRunsSkipStory(string flag) =>
            Assert.That(StorySequence.ShouldSkip(new[] { "Tiffany", flag }, false), Is.True);

        [TestCase(false)]
        [TestCase(true)]
        public void TestsAndBatchModeSkipWithoutCommandLineFlags(bool automatic) =>
            Assert.That(StorySequence.ShouldSkip(Array.Empty<string>(), automatic), Is.EqualTo(automatic));

        [Test]
        public void InterfaceProvidesThreeRequestedStoryLines()
        {
            Assert.That(InterfaceTexts.StoryLine(0), Is.EqualTo(
                "Hace tres meses, Tiffany siguió el ladrido de su perro hasta el bosque. Nunca volvió."));
            Assert.That(InterfaceTexts.StoryLine(1), Is.EqualTo(
                "Esta noche, Día de Muertos, su familia le encendió una veladora."));
            Assert.That(InterfaceTexts.StoryLine(2), Is.EqualTo("Su alma despertó. La bruja también."));
            Assert.Throws<ArgumentOutOfRangeException>(() => InterfaceTexts.StoryLine(StorySequence.LineCount));
        }
    }
}
