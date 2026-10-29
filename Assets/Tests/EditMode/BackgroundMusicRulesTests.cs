using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class BackgroundMusicRulesTests
    {
        private BackgroundMusicRules rules;
        [SetUp] public void SetUp() => rules = new BackgroundMusicRules();
        [Test]
        public void TitleFadesThemeInOverThreeSeconds()
        {
            rules.Tick(1.5f, InterfaceScreen.Title, -1, 7, false);
            Assert.AreEqual((BackgroundCue.Title, BackgroundTrack.Theme, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(1.5f, InterfaceScreen.Title, -1, 7, false);
            Assert.AreEqual(1f, rules.Volume);
        }
        [Test]
        public void StartingInCemeteryKeepsThemeAtFullVolume()
        {
            rules.Tick(3f, InterfaceScreen.Title, 0, 7, false);
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual((BackgroundCue.Opening, BackgroundTrack.Theme, 1f, false), (rules.Cue, rules.Track, rules.Volume, rules.OpeningOver));
        }
        [TestCase(-1)] [TestCase(1)]
        public void LeavingCemeteryEndsOpeningAndFadesOutOverFourSeconds(int clearing)
        {
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(2f, InterfaceScreen.Playing, clearing, 7, false);
            Assert.AreEqual((BackgroundCue.Silent, 0.5f, true), (rules.Cue, rules.Volume, rules.OpeningOver));
            rules.Tick(2f, InterfaceScreen.Playing, 1, 7, false);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual((BackgroundCue.Silent, 0f, true), (rules.Cue, rules.Volume, rules.OpeningOver));
        }
        [Test]
        public void StartingBeyondCemeteryPermanentlyEndsOpening()
        {
            rules.Tick(1f, InterfaceScreen.Playing, 1, 7, false);
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual((BackgroundCue.Silent, 0f, true), (rules.Cue, rules.Volume, rules.OpeningOver));
        }
        [Test]
        public void TensionFadesOutOverOneSecondAndWaitsForSixSecondsOfCalm()
        {
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(0.5f, InterfaceScreen.Playing, 0, 7, true);
            Assert.AreEqual(0.5f, rules.Volume);
            rules.Tick(0.5f, InterfaceScreen.Playing, 0, 7, true);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(5.9f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(0.2f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0.1f / BackgroundMusicRules.FadeIn, rules.Volume, 1e-5f);
        }
        [Test]
        public void RenewedTensionRestartsCalmCountdown()
        {
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(5f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(0.1f, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(5.9f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(0.4f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0.1f, rules.Volume, 1e-5f);
        }
        [TestCase(7, 7, BackgroundCue.House, BackgroundTrack.Finale)]
        [TestCase(0, -1, BackgroundCue.Opening, BackgroundTrack.Theme)]
        [TestCase(0, 0, BackgroundCue.Opening, BackgroundTrack.Theme)]
        [TestCase(1, 7, BackgroundCue.Silent, BackgroundTrack.Theme)]
        public void OnlyLastClearingOfAMultiClearingRouteUsesFinale(int clearing, int last, BackgroundCue cue, BackgroundTrack track)
        {
            rules.Tick(3f, InterfaceScreen.Playing, clearing, last, false);
            Assert.AreEqual((cue, track), (rules.Cue, rules.Track));
        }
        [Test]
        public void ChangingTracksFadesCurrentTrackToZeroBeforeSwitching()
        {
            rules.Tick(3f, InterfaceScreen.Title, 0, 7, false);
            rules.Tick(2f, InterfaceScreen.Ending, 7, 7, false);
            Assert.AreEqual((BackgroundCue.Ending, BackgroundTrack.Theme, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(2f, InterfaceScreen.Ending, 7, 7, false);
            Assert.AreEqual((BackgroundTrack.Finale, 0f), (rules.Track, rules.Volume));
            rules.Tick(3f, InterfaceScreen.Ending, 7, 7, false);
            Assert.AreEqual((BackgroundTrack.Finale, 1f), (rules.Track, rules.Volume));
        }
        [TestCase(InterfaceScreen.Title, BackgroundTrack.Theme)]
        [TestCase(InterfaceScreen.Ending, BackgroundTrack.Finale)]
        public void FrozenScreensIgnoreTensionAndItsRest(InterfaceScreen screen, BackgroundTrack track)
        {
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(3f, screen, 7, 7, true);
            Assert.AreEqual((track, 1f), (rules.Track, rules.Volume));
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0f, rules.Volume);
        }
        [TestCase(InterfaceScreen.Paused)] [TestCase(InterfaceScreen.Notebook)]
        public void PauseFreezesVolumeTrackAndOpeningProgress(InterfaceScreen screen)
        {
            rules.Tick(1.5f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(10f, screen, 7, 7, true);
            Assert.AreEqual((true, BackgroundTrack.Theme, 0.5f, false), (rules.Paused, rules.Track, rules.Volume, rules.OpeningOver));
            rules.Tick(1.5f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual((false, BackgroundCue.Opening, 1f), (rules.Paused, rules.Cue, rules.Volume));
        }
        [TestCase(InterfaceScreen.Paused)] [TestCase(InterfaceScreen.Notebook)]
        public void PauseDoesNotAdvanceCalmCountdown(InterfaceScreen screen)
        {
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(10f, screen, 7, 7, false);
            rules.Tick(2.9f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(0.4f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0.1f, rules.Volume, 1e-5f);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaUpdatesCueAndPauseWithoutAdvancingState(float dt)
        {
            rules.Tick(1.5f, InterfaceScreen.Title, 0, 7, false);
            rules.Tick(dt, InterfaceScreen.Ending, 7, 7, true);
            Assert.AreEqual((BackgroundCue.Ending, BackgroundTrack.Theme, 0.5f, false), (rules.Cue, rules.Track, rules.Volume, rules.OpeningOver));
            rules.Tick(dt, InterfaceScreen.Notebook, 7, 7, false);
            Assert.IsTrue(rules.Paused);
            rules.Tick(dt, InterfaceScreen.Playing, 7, 7, true);
            Assert.AreEqual((BackgroundCue.House, false, false), (rules.Cue, rules.Paused, rules.OpeningOver));
            rules.Tick(1.5f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual((BackgroundCue.Opening, 1f), (rules.Cue, rules.Volume));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaCannotSelectTrackOrRememberCemetery(float dt)
        {
            rules.Tick(dt, InterfaceScreen.Ending, 7, 7, false);
            Assert.AreEqual((BackgroundTrack.Theme, 0f), (rules.Track, rules.Volume));
            rules.Tick(dt, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(1f, InterfaceScreen.Playing, -1, 7, false);
            Assert.IsFalse(rules.OpeningOver);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaCannotAdvanceOrResetCalmCountdown(float dt)
        {
            rules.Tick(1f, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(3f, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(dt, InterfaceScreen.Playing, 0, 7, false);
            rules.Tick(dt, InterfaceScreen.Playing, 0, 7, true);
            rules.Tick(2.9f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(0.4f, InterfaceScreen.Playing, 0, 7, false);
            Assert.AreEqual(0.1f, rules.Volume, 1e-5f);
        }
        [Test]
        public void MissingRouteStaysSilentWithoutEndingOpening()
        {
            rules.Tick(3f, InterfaceScreen.Playing, -1, -1, false);
            rules.Tick(10f, InterfaceScreen.Playing, -1, -1, false);
            Assert.AreEqual((BackgroundCue.Silent, 0f, false), (rules.Cue, rules.Volume, rules.OpeningOver));
        }
    }
}
