using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class MemorySlideshowTests
    {
        static MemoryData Memory(int count = 3) => new MemoryData { id = 1, drawings = new MemoryDrawing[count] };

        [TestCase(1024, 768, "memory-1-1", false)]
        [TestCase(0, 0, null, true)]
        [TestCase(1, 1, "memory-1-1", true)]
        [TestCase(1, 768, "memory-1-1", true)]
        [TestCase(1024, 1, "memory-1-1", true)]
        [TestCase(640, 480, "memory-placeholder", true)]
        [TestCase(640, 480, "Placeholder-1", true)]
        public void PhraseIsOnlyShownWhenDrawingIsMissingOrPlaceholder(int width, int height, string name, bool expected)
        {
            Assert.AreEqual(expected, MemorySlideshow.ShouldShowPhrase(width, height, name));
        }

        [Test] public void FadesToBlackBeforeShowingPagesAndBackBeforeFinishing()
        {
            var show = new MemorySlideshow(Memory(1));
            Assert.AreEqual(0f, show.BlackOpacity);
            show.Tick(0.2f, true);
            Assert.AreEqual(0.5f, show.BlackOpacity, 0.001f);
            Assert.IsFalse(show.PageVisible);
            show.Tick(0.2f, true);
            Assert.IsTrue(show.PageVisible);
            Assert.AreEqual(1f, show.BlackOpacity);
            show.Tick(4f);
            Assert.AreEqual(MemorySlideshowStage.FadeIn, show.Stage);
            Assert.IsFalse(show.PageVisible);
            Assert.IsFalse(show.Finished);
            show.Tick(0.25f, true);
            Assert.AreEqual(0.5f, show.BlackOpacity, 0.001f);
            show.Tick(0.25f);
            Assert.IsTrue(show.Finished);
            Assert.AreEqual(0f, show.BlackOpacity);
        }

        [Test] public void EachPageLastsFourSecondsAndLongTicksCarryRemainingTime()
        {
            var show = new MemorySlideshow(Memory());
            show.Tick(0.4f);
            show.Tick(3.9f);
            Assert.AreEqual(0, show.PageIndex);
            show.Tick(0.2f);
            Assert.AreEqual(1, show.PageIndex);
            show.Tick(3.9f);
            Assert.AreEqual(2, show.PageIndex);
            show.Tick(4.5f);
            Assert.IsTrue(show.Finished);
            float elapsed = show.Elapsed;
            show.Tick(100f, true);
            Assert.AreEqual(elapsed, show.Elapsed);
        }

        [Test] public void ManualAdvanceResetsPageTimeAndDoesNotSkipTwoAtTimeout()
        {
            var show = new MemorySlideshow(Memory());
            show.Tick(0.4f);
            show.Tick(1f, true);
            Assert.AreEqual(1, show.PageIndex);
            show.Tick(3.9f);
            Assert.AreEqual(1, show.PageIndex);
            show.Tick(0.2f, true);
            Assert.AreEqual(2, show.PageIndex);
            show.Tick(0f, true);
            Assert.AreEqual(MemorySlideshowStage.FadeIn, show.Stage);
            show.Tick(0.5f);
            Assert.IsTrue(show.Finished);
        }

        [Test] public void SoundsAtZeroAndCrossedTimesPlayOnlyOnceWithTheirFades()
        {
            var sounds = new[]
            {
                new EchoSound { time = 0f, duration = 3f, pitch = 1f, volume = 0.8f, fadeIn = 0.4f },
                new EchoSound { time = 1f, duration = 2f, pitch = 1.5f, volume = 0.6f, fadeOut = 0.5f }
            };
            var audio = new EchoAudioEvaluator(sounds, new[] { 3f, 3f }, 2);
            var show = new MemorySlideshow(Memory(), audio);
            Assert.IsTrue(audio[0].Play);
            show.Tick(0.2f);
            Assert.IsFalse(audio[0].Play);
            Assert.AreEqual(0.4f, audio[0].Volume, 0.001f);
            show.Tick(1f);
            Assert.IsTrue(audio[1].Play);
            Assert.AreEqual(1, audio[1].SoundIndex);
            Assert.AreEqual(1.5f, audio[1].Pitch);
            show.Tick(0f, true);
            Assert.IsFalse(audio[1].Play);
            show.Tick(1.55f);
            Assert.IsFalse(audio[1].Play);
            Assert.AreEqual(0.3f, audio[1].Volume, 0.001f);
            show.Tick(0.3f);
            Assert.IsTrue(audio[1].Stop);
        }

        [Test] public void MusicFadesInHoldsAndFadesOutBeforeFinishing()
        {
            var show = new MemorySlideshow(Memory(1));
            Assert.AreEqual(0f, show.MusicVolume);
            show.Tick(0.25f);
            Assert.AreEqual(0.175f, show.MusicVolume, 0.001f);
            show.Tick(0.25f);
            Assert.AreEqual(0.35f, show.MusicVolume, 0.001f);
            show.Tick(1.9f);
            Assert.AreEqual(0.35f, show.MusicVolume, 0.001f);
            show.Tick(2f);
            Assert.AreEqual(0.35f, show.MusicVolume, 0.001f);
            show.Tick(0.25f);
            Assert.AreEqual(0.175f, show.MusicVolume, 0.001f);
            show.Tick(0.25f);
            Assert.IsTrue(show.Finished);
            Assert.AreEqual(0f, show.MusicVolume);
        }

        [Test] public void MissingMemoryOrDrawingsFinishesWithoutStartingSounds()
        {
            foreach (var memory in new[] { null, new MemoryData(), Memory(0) })
            {
                var audio = new EchoAudioEvaluator(new[] { new EchoSound { duration = 1f, pitch = 1f } }, new[] { 1f }, 1);
                var show = new MemorySlideshow(memory, audio);
                show.Tick(10f, true);
                Assert.IsTrue(show.Finished);
                Assert.IsFalse(audio[0].Play);
                Assert.AreEqual(0f, show.Elapsed);
            }
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotAdvance(float seconds)
        {
            var show = new MemorySlideshow(Memory());
            show.Tick(seconds, true);
            Assert.AreEqual(0f, show.Elapsed);
            Assert.AreEqual(MemorySlideshowStage.FadeOut, show.Stage);
        }
    }
}
