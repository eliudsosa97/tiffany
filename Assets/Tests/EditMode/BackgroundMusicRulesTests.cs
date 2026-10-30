using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class BackgroundMusicRulesTests
    {
        private BackgroundMusicRules rules;
        [SetUp] public void SetUp() => rules = new BackgroundMusicRules();
        [TestCase(InterfaceScreen.Title, BackgroundCue.Title, BackgroundTrack.Theme)]
        [TestCase(InterfaceScreen.Ending, BackgroundCue.Ending, BackgroundTrack.Finale)]
        public void TitleAndEndingFadeInOverThreeSeconds(InterfaceScreen screen, BackgroundCue cue, BackgroundTrack track)
        {
            rules.Tick(1.5f, screen);
            Assert.AreEqual((cue, track, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(1.5f, screen);
            Assert.AreEqual(1f, rules.Volume);
        }
        [Test]
        public void StartingGameStopsTitleImmediatelyAndNeverRestartsIt()
        {
            rules.Tick(3f, InterfaceScreen.Title);
            for (int frame = 0; frame < 1000; frame++)
            {
                rules.Tick(0.1f, InterfaceScreen.Playing);
                Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
            }
        }
        [TestCase(InterfaceScreen.Playing)] [TestCase(InterfaceScreen.Story)]
        public void GameplayAndStoryStaySilentWithoutAnExplicitMemory(InterfaceScreen screen)
        {
            rules.Tick(100f, screen);
            Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
        }
        [Test]
        public void ChangingTitleToEndingFadesCurrentTrackToZeroBeforeSwitching()
        {
            rules.Tick(3f, InterfaceScreen.Title);
            rules.Tick(2f, InterfaceScreen.Ending);
            Assert.AreEqual((BackgroundCue.Ending, BackgroundTrack.Theme, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(2f, InterfaceScreen.Ending);
            Assert.AreEqual((BackgroundTrack.Finale, 0f), (rules.Track, rules.Volume));
            rules.Tick(3f, InterfaceScreen.Ending);
            Assert.AreEqual((BackgroundTrack.Finale, 1f), (rules.Track, rules.Volume));
        }
        [Test]
        public void ExplicitMemoryFadesInAndOutOverHalfASecondThenStaysSilent()
        {
            rules.PlayMemory();
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Memory, BackgroundTrack.Memory, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual(1f, rules.Volume);
            rules.StopMemory();
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Silent, 0.5f), (rules.Cue, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(100f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
        }
        [Test]
        public void RepeatedMemoryRequestsDoNotResetTheFade()
        {
            rules.PlayMemory();
            rules.Tick(0.25f, InterfaceScreen.Playing);
            rules.PlayMemory();
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual(1f, rules.Volume);
        }
        [TestCase(InterfaceScreen.Title)] [TestCase(InterfaceScreen.Ending)]
        public void ReturningToTitleOrEndingClearsMemoryRequest(InterfaceScreen screen)
        {
            rules.PlayMemory();
            rules.Tick(3f, screen);
            Assert.AreEqual(1f, rules.Volume);
            rules.Tick(1f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
        }
        [Test]
        public void NotebookFreezesMemoryFadeAndResumesWithoutChangingTracks()
        {
            rules.PlayMemory();
            rules.Tick(0.25f, InterfaceScreen.Playing);
            rules.Tick(10f, InterfaceScreen.Notebook);
            Assert.AreEqual((true, BackgroundCue.Memory, BackgroundTrack.Memory, 0.5f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((false, BackgroundCue.Memory, 1f), (rules.Paused, rules.Cue, rules.Volume));
        }
        [Test]
        public void NotebookDuringGameplayNeverStartsMusic()
        {
            rules.Tick(3f, InterfaceScreen.Title);
            rules.Tick(0.1f, InterfaceScreen.Playing);
            rules.Tick(10f, InterfaceScreen.Notebook);
            Assert.AreEqual((true, BackgroundCue.Silent, 0f), (rules.Paused, rules.Cue, rules.Volume));
            rules.Tick(10f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
        }
        [Test]
        public void PauseFadesThemeInOverHalfASecond()
        {
            rules.Tick(0.1f, InterfaceScreen.Playing);
            rules.Tick(0.25f, InterfaceScreen.Paused);
            Assert.AreEqual((false, BackgroundCue.Pause, BackgroundTrack.Theme, 0.5f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Paused);
            Assert.AreEqual(1f, rules.Volume);
        }
        [Test]
        public void ResumingGameplayFadesPauseThemeOutOverHalfASecondThenStaysSilent()
        {
            rules.Tick(0.5f, InterfaceScreen.Paused);
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((false, BackgroundCue.Silent, BackgroundTrack.Theme, 0.5f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual(0f, rules.Volume);
            rules.Tick(100f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Silent, 0f), (rules.Cue, rules.Volume));
        }
        [Test]
        public void NotebookFreezesPauseThemeFade()
        {
            rules.Tick(0.25f, InterfaceScreen.Paused);
            rules.Tick(10f, InterfaceScreen.Notebook);
            Assert.AreEqual((true, BackgroundCue.Pause, BackgroundTrack.Theme, 0.5f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Paused);
            Assert.AreEqual((false, 1f), (rules.Paused, rules.Volume));
        }
        [Test]
        public void ActiveMemorySurvivesPausingAndResuming()
        {
            rules.PlayMemory();
            rules.Tick(0.5f, InterfaceScreen.Playing);
            rules.Tick(0.25f, InterfaceScreen.Paused);
            Assert.AreEqual((BackgroundCue.Pause, BackgroundTrack.Memory, 0.5f),
                (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Paused);
            Assert.AreEqual((BackgroundTrack.Theme, 0f), (rules.Track, rules.Volume));
            rules.Tick(0.5f, InterfaceScreen.Paused);
            Assert.AreEqual((BackgroundTrack.Theme, 1f), (rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundCue.Memory, BackgroundTrack.Theme, 0.5f),
                (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(0.25f, InterfaceScreen.Playing);
            Assert.AreEqual((BackgroundTrack.Memory, 0f), (rules.Track, rules.Volume));
            rules.Tick(0.5f, InterfaceScreen.Playing);
            Assert.AreEqual((false, BackgroundCue.Memory, BackgroundTrack.Memory, 1f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaUpdatesCueAndPauseWithoutAdvancingFades(float dt)
        {
            rules.Tick(1.5f, InterfaceScreen.Title);
            rules.Tick(dt, InterfaceScreen.Ending);
            Assert.AreEqual((BackgroundCue.Ending, BackgroundTrack.Theme, 0.5f), (rules.Cue, rules.Track, rules.Volume));
            rules.Tick(dt, InterfaceScreen.Paused);
            Assert.AreEqual((false, BackgroundCue.Pause, BackgroundTrack.Theme, 0.5f),
                (rules.Paused, rules.Cue, rules.Track, rules.Volume));
            rules.Tick(dt, InterfaceScreen.Notebook);
            Assert.IsTrue(rules.Paused);
            rules.Tick(0.1f, InterfaceScreen.Playing);
            Assert.AreEqual((false, BackgroundCue.Silent, 0f), (rules.Paused, rules.Cue, rules.Volume));
        }
    }
}
