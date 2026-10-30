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
        [TestCase(3, Chapter.Cemetery)]
        [TestCase(4, Chapter.Cemetery)]
        [TestCase(5, Chapter.Cemetery)]
        [TestCase(6, Chapter.Cemetery)]
        [TestCase(7, Chapter.Cemetery)]
        [TestCase(8, Chapter.Cemetery)]
        public void CemeteryOnlyAdvancesAtTheWaterPlant(int clearing, Chapter expected) =>
            Assert.AreEqual(expected, RouteChapters.Resolve(Chapter.Cemetery, clearing),
                "La caja de un claro lejano puede tapar el cementerio y no debe adelantar el capítulo.");

        [TestCase(-1, -1, false)]
        [TestCase(-1, 0, true)]
        [TestCase(-1, 1, false)]
        [TestCase(-1, 6, false)]
        [TestCase(0, 6, false)]
        [TestCase(1, 3, false)]
        [TestCase(1, 2, true)]
        [TestCase(2, 0, true)]
        public void VisitsAcceptOnlyPreviousClearingsOrTheNextOne(int lastVisited, int clearing, bool expected) =>
            Assert.AreEqual(expected, RouteChapters.CanVisitClearing(lastVisited, clearing));

        [Test]
        public void OverlappingRoadBoundsCannotAdvanceVisitsOrUnlockLanding()
        {
            var bounds = new Bounds[8];
            bounds[6] = new Bounds(Vector3.zero, Vector3.one * 500f);
            int visited = 0;
            Chapter chapter = Chapter.Cemetery;
            for (int frame = 0; frame < 10; frame++)
            {
                int clearing = RouteChapters.FindClearing(Vector3.zero, bounds);
                if (RouteChapters.CanVisitClearing(visited, clearing)) visited = clearing;
                chapter = RouteChapters.Resolve(chapter, clearing + 1);
            }
            Assert.AreEqual(0, visited);
            Assert.AreEqual(Chapter.Cemetery, chapter);
            Assert.IsFalse(WitchProfiles.For(chapter).CanLand);
        }

        [Test]
        public void BacktrackingKeepsTheHighestChapter()
        {
            var chapter = RouteChapters.Resolve(Chapter.Cemetery, 2);
            chapter = RouteChapters.Resolve(chapter, 3);
            chapter = RouteChapters.Resolve(chapter, 4);
            for (int clearing = 3; clearing >= 1; clearing--)
            {
                chapter = RouteChapters.Resolve(chapter, clearing);
                Assert.AreEqual(Chapter.Stable, chapter, "Antes del establo se visitan la planta y las vías en orden.");
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
