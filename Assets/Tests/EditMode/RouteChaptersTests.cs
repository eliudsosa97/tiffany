using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class RouteChaptersTests
    {
        [TestCase(0f, 0f, 0f, 1)]
        [TestCase(-2f, 50f, -3f, 1)]
        [TestCase(2f, -50f, 3f, 1)]
        [TestCase(-2.01f, 0f, 0f, -1)]
        [TestCase(2.01f, 0f, 0f, -1)]
        [TestCase(0f, 0f, -3.01f, -1)]
        [TestCase(0f, 0f, 3.01f, -1)]
        [TestCase(10f, 0f, 0f, 2)]
        public void ClearingUsesHorizontalBoundsAndIncludesEdges(float x, float y, float z, int expected)
        {
            var bounds = new[] { default(Bounds), new Bounds(Vector3.zero, new Vector3(4f, 1f, 6f)),
                new Bounds(Vector3.right * 10f, Vector3.one * 2f) };
            Assert.AreEqual(expected, RouteChapters.FindClearing(new Vector3(x, y, z), bounds));
        }

        [Test]
        public void OverlappingClearingsUseTheFirstMatch()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            Assert.AreEqual(0, RouteChapters.FindClearing(Vector3.zero, new[] { bounds, bounds }));
        }

        [Test]
        public void MissingOrEmptyBoundsDoNotMatch()
        {
            Assert.AreEqual(-1, RouteChapters.FindClearing(Vector3.zero, null));
            Assert.AreEqual(-1, RouteChapters.FindClearing(Vector3.zero, System.Array.Empty<Bounds>()));
            Assert.AreEqual(-1, RouteChapters.FindClearing(Vector3.zero, new Bounds[1]));
        }

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
