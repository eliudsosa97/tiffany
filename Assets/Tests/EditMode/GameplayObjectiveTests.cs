using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class GameplayObjectiveTests
    {
        static MemoryProgress NewProgress() => new MemoryProgress(new[] { 1, MemoryProgress.BallId, 2, 3, 4, 5 });

        [Test]
        public void StartsAtGraveAndAdvancesThroughBallToBirds()
        {
            var progress = NewProgress();
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(InterfaceTexts.GraveObjective));
            Assert.That(GameplayObjective.Text(progress), Does.Contain(GameInput.InteractBinding));
            progress.MarkFound(1);
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(InterfaceTexts.BallObjective));
            progress.MarkFound(MemoryProgress.BallId);
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(InterfaceTexts.BirdsObjective));
        }

        [TestCase(false, false, InterfaceTexts.GraveObjective)]
        [TestCase(true, false, InterfaceTexts.BallObjective)]
        [TestCase(true, true, InterfaceTexts.BirdsObjective)]
        [TestCase(false, true, InterfaceTexts.GraveObjective)]
        public void DerivesObjectiveFromRestoredProgress(bool grave, bool ball, string expected)
        {
            var progress = NewProgress();
            if (grave) progress.MarkFound(1);
            if (ball) progress.MarkFound(MemoryProgress.BallId);
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(expected));
        }

        [Test]
        public void LaterMemoriesKeepBirdsAndClearingProgressReturnsToGrave()
        {
            var progress = NewProgress();
            foreach (int id in new[] { 1, MemoryProgress.BallId, 2, 3, 4, 5 }) progress.MarkFound(id);
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(InterfaceTexts.BirdsObjective));
            progress.Clear();
            Assert.That(GameplayObjective.Text(progress), Is.EqualTo(InterfaceTexts.GraveObjective));
            Assert.That(GameplayObjective.Text(null), Is.EqualTo(InterfaceTexts.GraveObjective));
        }
    }
}
