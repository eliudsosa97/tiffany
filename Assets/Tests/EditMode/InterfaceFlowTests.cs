using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class InterfaceFlowTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TitleOffersContinueOnlyWithValidSave(bool validSave)
        {
            var flow = new InterfaceFlow(true);
            flow.ConfigureTitle(validSave);
            Assert.AreEqual(validSave, flow.HasContinue);
            Assert.AreEqual(validSave, flow.ContinueSelected);
            flow.SelectTitleOption(false);
            Assert.IsFalse(flow.ContinueSelected);
            flow.SelectTitleOption(true);
            Assert.AreEqual(validSave, flow.ContinueSelected);
            Assert.AreEqual(InterfaceScreen.Title, flow.Screen);
        }

        [TestCase(false, InterfaceScreen.Playing)]
        [TestCase(true, InterfaceScreen.Title)]
        public void ConstructorSelectsInitialScreen(bool startOnTitle, InterfaceScreen screen)
        {
            var flow = new InterfaceFlow(startOnTitle);
            Assert.That(flow.Screen, Is.EqualTo(screen));
            Assert.That(flow.IsFrozen, Is.EqualTo(startOnTitle));
        }

        [TestCase("")]
        [TestCase("P")]
        [TestCase("N")]
        [TestCase("PNNP")]
        public void TitleStaysFrozenUntilStart(string inputs)
        {
            var flow = new InterfaceFlow(true);
            foreach (char input in inputs)
            {
                if (input == 'P') flow.PressPause();
                else flow.PressNotebook();
            }
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Title));
            Assert.That(flow.IsFrozen, Is.True);
            flow.PressStart();
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Playing));
            Assert.That(flow.IsFrozen, Is.False);
        }

        [TestCase(InterfaceScreen.Playing)]
        [TestCase(InterfaceScreen.Paused)]
        [TestCase(InterfaceScreen.Notebook)]
        [TestCase(InterfaceScreen.Ending)]
        public void StartDoesNothingOutsideTitle(InterfaceScreen screen)
        {
            var flow = new InterfaceFlow();
            if (screen == InterfaceScreen.Paused) flow.PressPause();
            if (screen == InterfaceScreen.Notebook) flow.PressNotebook();
            if (screen == InterfaceScreen.Ending) flow.SetEnding(true);
            flow.PressStart();
            Assert.That(flow.Screen, Is.EqualTo(screen));
            Assert.That(flow.IsFrozen, Is.EqualTo(screen != InterfaceScreen.Playing));
        }

        [Test]
        public void InactiveEndingDoesNotDismissTitle()
        {
            var flow = new InterfaceFlow(true);
            flow.SetEnding(false);
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Title));
            Assert.That(flow.IsFrozen, Is.True);
        }

        [TestCase("", InterfaceScreen.Playing, false)]
        [TestCase("P", InterfaceScreen.Paused, true)]
        [TestCase("PP", InterfaceScreen.Playing, false)]
        [TestCase("N", InterfaceScreen.Notebook, true)]
        [TestCase("NN", InterfaceScreen.Playing, false)]
        [TestCase("NP", InterfaceScreen.Playing, false)]
        [TestCase("PN", InterfaceScreen.Paused, true)]
        public void InputSequenceSelectsScreenAndFreezeState(string inputs, InterfaceScreen screen, bool frozen)
        {
            var flow = new InterfaceFlow();
            foreach (char input in inputs)
            {
                if (input == 'P') flow.PressPause();
                else flow.PressNotebook();
            }
            Assert.That(flow.Screen, Is.EqualTo(screen));
            Assert.That(flow.IsFrozen, Is.EqualTo(frozen));
        }

        [TestCase(Power.Light, "Tu luz te obedece · F prende y apaga, clic para el destello")]
        [TestCase(Power.Candles, "Puedes encender veladoras · Q")]
        [TestCase(Power.Poltergeist, "Puedes mover cosas · E")]
        [TestCase(Power.Vanish, "Puedes desvanecerte · R")]
        [TestCase(Power.Manifest, "Tu familia puede verte")]
        public void PowerNoticeMatchesPower(Power power, string expected) =>
            Assert.That(InterfaceTexts.PowerNotice(power), Is.EqualTo(expected));

        [Test]
        public void PowerNoticeLastsThreeSeconds() =>
            Assert.That(InterfaceTexts.PowerNoticeSeconds, Is.EqualTo(3f));

        [Test]
        public void PowerNoticeRejectsUnknownPower() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => InterfaceTexts.PowerNotice((Power)99));
    }
}
