using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class TimedLineTests
    {
        TimedLine line;
        [SetUp] public void SetUp() => line = new TimedLine();

        [Test] public void StartsHiddenAndStaysUnchangedWhenIdle()
        {
            AssertLine(null, 0);
            line.Tick(5f);
            line.Clear();
            AssertLine(null, 0);
        }

        [Test] public void ShowsImmediatelyAndExpiresAtExactDuration()
        {
            line.Enqueue("Primera", 2f);
            AssertLine("Primera", 1);
            line.Tick(1.5f);
            AssertLine("Primera", 1);
            line.Tick(0.5f);
            AssertLine(null, 2);
        }

        [TestCase("Segunda", 1f, 2), TestCase("Segunda", 1.5f, 2), TestCase("Primera", 1f, 1)]
        public void QueueCarriesTimeAndVersionsOnlyTextChanges(string nextText, float elapsed, int version)
        {
            line.Enqueue("Primera", 1f);
            line.Tick(0.5f);
            line.Enqueue(nextText, 2f);
            AssertLine("Primera", 1);
            line.Tick(elapsed - 0.5f);
            AssertLine(nextText, version);
            line.Tick(2.5f - elapsed);
            AssertLine(nextText, version);
            line.Tick(0.5f);
            AssertLine(null, version + 1);
            line.Tick(1f);
            AssertLine(null, version + 1);
        }

        [Test] public void LargeTickConsumesWholeQueueAndAllowsNewLine()
        {
            line.Enqueue("Primera", 1f);
            line.Enqueue("Segunda", 2f);
            line.Enqueue("Tercera", 1f);
            line.Tick(10f);
            AssertLine(null, 4);
            line.Enqueue("Nueva", 1f);
            line.Tick(0.5f);
            AssertLine("Nueva", 5);
        }

        [TestCase(null, 1f), TestCase("", 1f), TestCase("Inválida", 0f), TestCase("Inválida", -1f)]
        public void IgnoresEmptyTextAndNonPositiveDuration(string text, float seconds)
        {
            line.Enqueue(text, seconds);
            AssertLine(null, 0);
            line.Enqueue("Válida", 1f);
            line.Enqueue(text, seconds);
            line.Tick(1f);
            AssertLine(null, 2);
        }

        [Test] public void ClearHidesTextAndDiscardsQueue()
        {
            line.Enqueue("Primera", 1f);
            line.Enqueue("Segunda", 1f);
            line.Clear();
            AssertLine(null, 2);
            line.Tick(10f);
            line.Clear();
            AssertLine(null, 2);
            line.Enqueue("Nueva", 1f);
            line.Tick(1f);
            AssertLine(null, 4);
        }

        [TestCase(0f), TestCase(-1f)]
        public void NonPositiveTickDoesNotAdvanceOrExtendLine(float dt)
        {
            line.Enqueue("Primera", 1f);
            line.Tick(dt);
            AssertLine("Primera", 1);
            line.Tick(1f);
            AssertLine(null, 2);
        }

        void AssertLine(string text, int version) =>
            Assert.That((line.Text, line.IsVisible, line.Version), Is.EqualTo((text, text != null, version)));
    }
}
