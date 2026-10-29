using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class StormScheduleTests
    {
        [TestCase(Chapter.Cemetery, false)]
        [TestCase(Chapter.WaterPlant, false)]
        [TestCase(Chapter.TrainTracks, false)]
        [TestCase(Chapter.Stable, false)]
        [TestCase(Chapter.WitchHut, false)]
        [TestCase(Chapter.Return, true)]
        public void RainStartsOnlyInReturn(Chapter chapter, bool expected) =>
            Assert.AreEqual(expected, StormSchedule.IsRaining(chapter));

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void ReturnKeepsRainThroughHutLakeRoadAndHouse(int clearing) =>
            Assert.IsTrue(StormSchedule.IsRaining(RouteChapters.Resolve(Chapter.Return, clearing)));
    }
}
