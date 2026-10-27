using NUnit.Framework;

namespace ElVelador.Tests
{
    public class GameInfoTests
    {
        [Test]
        public void LogPrefixNamesTheGame()
        {
            Assert.AreEqual("[Velador]", GameInfo.LogPrefix);
        }
    }
}
