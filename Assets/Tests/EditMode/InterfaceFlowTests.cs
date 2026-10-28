using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class InterfaceFlowTests
    {
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

        [TestCase(Power.Light, "Tu luz te obedece")]
        [TestCase(Power.Candles, "Puedes encender veladoras")]
        [TestCase(Power.Poltergeist, "Puedes mover objetos")]
        [TestCase(Power.Vanish, "Puedes desvanecerte")]
        [TestCase(Power.Manifest, "Puedes manifestarte")]
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
