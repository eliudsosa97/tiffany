using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class RouteChaptersTests
    {
        [TestCase(1, Chapter.Cemetery)]
        [TestCase(2, Chapter.WaterPlant)]
        [TestCase(3, Chapter.TrainTracks)]
        [TestCase(4, Chapter.Stable)]
        [TestCase(5, Chapter.WitchHut)]
        [TestCase(6, Chapter.WitchHut)]
        [TestCase(7, Chapter.WitchHut)]
        [TestCase(8, Chapter.WitchHut)]
        public void ClearingSelectsItsChapter(int clearing, Chapter expected) =>
            Assert.AreEqual(expected, RouteChapters.Resolve(Chapter.Cemetery, clearing));

        [Test]
        public void BacktrackingKeepsTheHighestChapter()
        {
            var chapter = RouteChapters.Resolve(Chapter.Cemetery, 2);
            chapter = RouteChapters.Resolve(chapter, 4);
            for (int clearing = 3; clearing >= 1; clearing--)
            {
                chapter = RouteChapters.Resolve(chapter, clearing);
                Assert.AreEqual(Chapter.Stable, chapter);
            }
            chapter = RouteChapters.Resolve(chapter, 5);
            for (int clearing = 6; clearing <= 8; clearing++)
                Assert.AreEqual(Chapter.WitchHut, RouteChapters.Resolve(chapter, clearing));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(9)]
        public void PathsAndUnknownClearingsKeepTheChapter(int clearing)
        {
            Assert.AreEqual(Chapter.Cemetery, RouteChapters.Resolve(Chapter.Cemetery, clearing));
            Assert.AreEqual(Chapter.TrainTracks, RouteChapters.Resolve(Chapter.TrainTracks, clearing));
            Assert.AreEqual(Chapter.WitchHut, RouteChapters.Resolve(Chapter.WitchHut, clearing));
        }

        [Test]
        public void ReturnOverridesEveryClearingAndPath()
        {
            Assert.IsTrue(RouteProgress.StartsReturn(5));
            for (int clearing = 0; clearing <= 8; clearing++)
                Assert.AreEqual(Chapter.Return, RouteChapters.Resolve(Chapter.Return, clearing));
        }
    }
}
