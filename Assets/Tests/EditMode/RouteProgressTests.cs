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
    }
}
