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

        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        [TestCase(5, 0)]
        [TestCase(5, 7)]
        public void MemoriesNeverWaitForPowerChallenges(int memoryId, int completed) =>
            Assert.IsTrue(RouteProgress.CanCollectMemory(memoryId, completed));
    }
}
