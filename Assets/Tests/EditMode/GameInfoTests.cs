using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class GameInfoTests
    {
        [Test]
        public void LogPrefixNamesTheGame()
        {
            Assert.AreEqual("[Tiffany]", GameInfo.LogPrefix);
        }
    }
}
