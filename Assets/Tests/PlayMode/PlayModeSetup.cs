using NUnit.Framework;

namespace TiffanyGame.Tests
{
    [SetUpFixture]
    public sealed class PlayModeSetup
    {
        bool previousSkipTitle;

        [OneTimeSetUp]
        public void SetUp()
        {
            previousSkipTitle = GameInterface.SkipTitle;
            GameInterface.SkipTitle = true;
        }

        [OneTimeTearDown]
        public void TearDown() => GameInterface.SkipTitle = previousSkipTitle;
    }
}
