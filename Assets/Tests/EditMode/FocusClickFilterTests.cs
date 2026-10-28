using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public class FocusClickFilterTests
    {
        [Test]
        public void ClickWithoutFocusIsRejected()
        {
            var filter = new FocusClickFilter(false, 0f, false);

            Assert.That(filter.Accept(true, 1f), Is.False);
        }

        [TestCase(0f, false)]
        [TestCase(0.29f, false)]
        [TestCase(0.3f, true)]
        [TestCase(0.31f, true)]
        public void InitiallyFocusedClickUsesIgnoreWindow(float time, bool accepted)
        {
            var filter = new FocusClickFilter(true, 0f, false);

            Assert.That(filter.Accept(true, time), Is.EqualTo(accepted));
        }

        [TestCase(0f, false)]
        [TestCase(0.29f, false)]
        [TestCase(0.3f, true)]
        [TestCase(0.31f, true)]
        public void ClickAfterRegainingFocusUsesIgnoreWindow(float time, bool accepted)
        {
            var filter = new FocusClickFilter(false, -1f, false);
            filter.SetFocus(true, 0f);

            Assert.That(filter.Accept(true, time), Is.EqualTo(accepted));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoClickIsRejected(bool focused)
        {
            var filter = new FocusClickFilter(focused, 0f, false);

            Assert.That(filter.Accept(false, 1f), Is.False);
        }

        [Test]
        public void LosingAndRegainingFocusRestartsIgnoreWindow()
        {
            var filter = new FocusClickFilter(true, 0f, false);
            Assert.That(filter.Accept(true, 1f), Is.True);

            filter.SetFocus(false, 1f);
            Assert.That(filter.Accept(true, 2f), Is.False);

            filter.SetFocus(true, 2f);
            Assert.That(filter.Accept(true, 2f), Is.False);
            Assert.That(filter.Accept(true, 2.29f), Is.False);
            Assert.That(filter.Accept(true, 2.31f), Is.True);
        }

        [TestCase(1f)]
        [TestCase(60f)]
        public void InitiallyFocusedFilterConsumesOnlyFirstClickRegardlessOfDelay(float time)
        {
            var filter = new FocusClickFilter(true, 0f, true);

            Assert.That(filter.Accept(false, time), Is.False);
            Assert.That(filter.Accept(true, time), Is.False);
            Assert.That(filter.Accept(true, time + 1f), Is.True);
        }

        [Test]
        public void ConsumingFirstClickPreservesInitialIgnoreWindow()
        {
            var filter = new FocusClickFilter(true, 0f, true);

            Assert.That(filter.Accept(true, 0f), Is.False);
            Assert.That(filter.Accept(true, 0.29f), Is.False);
            Assert.That(filter.Accept(true, 0.3f), Is.True);
        }

        [Test]
        public void SettingFocusWhileAlreadyFocusedDoesNotRestartIgnoreWindow()
        {
            var filter = new FocusClickFilter(true, 0f, false);

            filter.SetFocus(true, 1f);

            Assert.That(filter.Accept(true, 1f), Is.True);
        }

        [Test]
        public void ClickWithoutFocusConsumesFirstClick()
        {
            var filter = new FocusClickFilter(false, 0f, true);

            Assert.That(filter.Accept(true, 1f), Is.False);
            Assert.That(filter.Accept(true, 1.5f), Is.False);

            filter.SetFocus(true, 2f);

            Assert.That(filter.Accept(true, 2.31f), Is.True);
        }
    }
}
