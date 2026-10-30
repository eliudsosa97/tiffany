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
        [TestCase(InterfaceScreen.Story)]
        public void StartDoesNothingOutsideTitle(InterfaceScreen screen)
        {
            var flow = new InterfaceFlow();
            if (screen == InterfaceScreen.Paused) flow.PressPause();
            if (screen == InterfaceScreen.Notebook) flow.PressNotebook();
            if (screen == InterfaceScreen.Ending) flow.SetEnding(true);
            if (screen == InterfaceScreen.Story)
            {
                flow = new InterfaceFlow(true);
                flow.PressStart(true);
            }
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

        [TestCase(Power.Light, "Clic / Triángulo", "prende o apaga")]
        [TestCase(Power.Light, "Mantén clic / Triángulo / F", "cegar")]
        [TestCase(Power.Light, "3 segundos", "360°")]
        [TestCase(Power.Candles, "Q / Cuadrado", "protege")]
        [TestCase(Power.Poltergeist, "E / Círculo", "distrae")]
        [TestCase(Power.Vanish, "R / R1", "pierde tu rastro")]
        [TestCase(Power.Manifest, "Espacio / Equis", "manifiéstate ante tu familia en tu casa")]
        public void PowerNoticeExplainsControlsAndPurpose(Power power, string controls, string purpose)
        {
            Assert.That(InterfaceTexts.PowerNotice(power), Does.Contain(controls).And.Contain(purpose));
            Assert.That(InterfaceTexts.PowerNotice(power), Does.Not.Contain("△").And.Not.Contain("□")
                .And.Not.Contain("○").And.Not.Contain("✕"));
        }

        [TestCase("WASD / stick izquierdo  moverte")]
        [TestCase("Ratón / stick derecho  apuntar la luz")]
        [TestCase("Shift / L3 / R2  correr")]
        [TestCase("Ctrl / L2  despacio")]
        [TestCase("Clic / Triángulo  prender la luz")]
        [TestCase("mantén clic / △ / F  destello")]
        [TestCase("Q / Cuadrado  encender veladora")]
        [TestCase("E / Círculo  mover objeto")]
        [TestCase("R / R1  desvanecerte")]
        [TestCase("Espacio / Equis  esconderte y recoger")]
        [TestCase("Tab / Create  cuaderno")]
        [TestCase("Esc o P / Options  pausa")]
        public void ControlsDescribeEachKeyboardAndDualSenseAction(string line) =>
            CollectionAssert.Contains(InterfaceTexts.Controls.Split('\n'), line);

        [TestCase(true, "△ □ ○ ✕")]
        [TestCase(false, "Triángulo Cuadrado Círculo Equis")]
        public void UnsupportedControllerSymbolsUseNames(bool hasSymbols, string expected) =>
            Assert.AreEqual(expected, InterfaceTexts.ControllerText("△ □ ○ ✕", hasSymbols));

        [TestCase("Clic / Triángulo")]
        [TestCase("mantén clic / Triángulo / F")]
        [TestCase("Q / Cuadrado")]
        [TestCase("E / Círculo")]
        [TestCase("R / R1")]
        [TestCase("Espacio / Equis")]
        [TestCase("Tab / Create")]
        [TestCase("Esc o P / Options")]
        public void ControlsExplainKeyboardAndDualSenseTogether(string controls) =>
            Assert.That(InterfaceTexts.ControllerText(InterfaceTexts.Controls, false), Does.Contain(controls));

        [Test]
        public void PowerNoticeLastsSevenSeconds() =>
            Assert.That(InterfaceTexts.PowerNoticeSeconds, Is.EqualTo(7f));

        [TestCase(false, true, InterfaceScreen.Story)]
        [TestCase(true, true, InterfaceScreen.Playing)]
        [TestCase(false, false, InterfaceScreen.Playing)]
        public void OnlyNewGameShowsStory(bool continuing, bool showStory, InterfaceScreen expected)
        {
            var flow = new InterfaceFlow(true);
            flow.ConfigureTitle(continuing);
            flow.PressStart(showStory);
            Assert.That(flow.Screen, Is.EqualTo(expected));
        }

        [Test]
        public void SelectingNewGameWithASaveShowsStory()
        {
            var flow = new InterfaceFlow(true);
            flow.ConfigureTitle(true);
            flow.SelectTitleOption(false);
            flow.PressStart(true);
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Story));
        }

        [Test]
        public void StoryBlocksPauseAndNotebookUntilFinished()
        {
            var flow = new InterfaceFlow(true);
            flow.FinishStory();
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Title));
            flow.PressStart(true);
            flow.PressPause();
            flow.PressNotebook();
            flow.SetEnding(false);
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Story));
            Assert.That(flow.IsFrozen, Is.True);
            flow.FinishStory();
            Assert.That(flow.Screen, Is.EqualTo(InterfaceScreen.Playing));
            Assert.That(flow.IsFrozen, Is.False);
        }

        [Test]
        public void PowerNoticeRejectsUnknownPower() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => InterfaceTexts.PowerNotice((Power)99));
    }
}
