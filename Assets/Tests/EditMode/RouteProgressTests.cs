using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class RouteProgressTests
    {
        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(99, false)]
        public void OnlyFifthMemoryStartsReturn(int memoryId, bool expected) =>
            Assert.AreEqual(expected, RouteProgress.StartsReturn(memoryId));

        [TestCase(2, 0, true)]
        [TestCase(3, 0, false)]
        [TestCase(3, 1, true)]
        [TestCase(4, 1, false)]
        [TestCase(4, 3, true)]
        [TestCase(5, 7, false)]
        [TestCase(5, 11, false)]
        [TestCase(5, 15, true)]
        public void MemoriesRequireThePreviousPowerChallenges(int memoryId, int completed, bool expected) =>
            Assert.AreEqual(expected, RouteProgress.CanCollectMemory(memoryId, completed));
    }
}
