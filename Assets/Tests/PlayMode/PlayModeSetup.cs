using NUnit.Framework;

namespace TiffanyGame.Tests
{
    [SetUpFixture]
    public sealed class PlayModeSetup
    {
        bool previousSkipTitle;
        bool previousSaveDisabled;
        string previousTestKey;

        [OneTimeSetUp]
        public void SetUp()
        {
            previousSkipTitle = GameInterface.SkipTitle;
            GameInterface.SkipTitle = true;
            previousSaveDisabled = SaveGame.DisabledForTests;
            previousTestKey = SaveGame.TestKey;
            SaveGame.DisabledForTests = true;
            SaveGame.TestKey = null;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            GameInterface.SkipTitle = previousSkipTitle;
            SaveGame.DisabledForTests = previousSaveDisabled;
            SaveGame.TestKey = previousTestKey;
        }
    }
}
