using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class TitleScreenTests
    {
        Mouse mouse;
        object previousFilter;
        readonly System.Reflection.FieldInfo filterField = typeof(GameInput).GetField("focusClickFilter",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInterface.SkipTitle = true;
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousFilter != null) filterField.SetValue(null, previousFilter);
            SceneManager.CreateScene(nameof(TitleScreenTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator TitleFreezesUntilTheFrameAfterStarting()
        {
            GameInterface.SkipTitle = false;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            var ui = Object.FindAnyObjectByType<GameInterface>();
            var light = Object.FindAnyObjectByType<PlayerLight>();
            Assert.That(ui.Screen, Is.EqualTo(InterfaceScreen.Title));
            Canvas.ForceUpdateCanvases();
            var controls = ui.titlePanel.transform.Find("Controls").GetComponent<UnityEngine.UI.Text>();
            var settings = controls.GetGenerationSettings(controls.rectTransform.rect.size);
            Assert.LessOrEqual(controls.cachedTextGenerator.GetPreferredHeight(controls.text, settings),
                controls.rectTransform.rect.height * controls.pixelsPerUnit);
            Assert.IsTrue(GameInterface.HasControllerSymbols(controls.font) || !controls.text.Contains("△"));
            Assert.That(ui.titlePanel.activeInHierarchy, Is.True);
            Assert.That(ui.storyPanel.activeSelf, Is.False);
            Assert.That(ui.soulPanel.activeSelf, Is.False);
            Assert.That(ui.endingPanel.gameObject.activeSelf, Is.False);
            Assert.That(ui.pausePanel.activeSelf, Is.False);
            Assert.That(ui.notebookPanel.activeSelf, Is.False);
            Assert.That(ui.noticeText.gameObject.activeSelf, Is.False);
            Assert.That(ui.subtitleText.gameObject.activeSelf, Is.False);
            Assert.That((ui.IsFrozen, Time.timeScale, AudioListener.pause), Is.EqualTo((true, 0f, true)));

            ui.ShowEnding(new EndingRules());
            Assert.That(ui.Screen, Is.EqualTo(InterfaceScreen.Title));
            Assert.That(ui.titlePanel.activeInHierarchy, Is.True);
            ui.TogglePause();
            ui.ToggleNotebook();
            Assert.That(ui.Screen, Is.EqualTo(InterfaceScreen.Title));
            mouse = InputSystem.AddDevice<Mouse>();
            previousFilter = filterField.GetValue(null);
            filterField.SetValue(null, new FocusClickFilter(true, float.NegativeInfinity, false));
            Canvas.ForceUpdateCanvases();
            if (ui.HasContinue) InputState.Change(mouse.position, RectTransformUtility.WorldToScreenPoint(null, ui.continueOption.rectTransform.position));
            InputState.Change(mouse.leftButton, 1f);
            ui.SendMessage("Update");
            GameInput.FlashOverride = true;
            light.SendMessage("Update");
            GameInput.FlashOverride = false;
            Assert.That(light.State.IsFlashing, Is.False);
            Assert.IsFalse(light.State.IsOn || GameInput.LightTogglePressed);
            Assert.That((ui.IsFrozen, Time.timeScale, AudioListener.pause), Is.EqualTo((true, 0f, true)));
            yield return null;

            Assert.That(ui.Screen, Is.EqualTo(InterfaceScreen.Playing));
            Assert.That(ui.storyPanel.activeSelf, Is.False);
            Assert.That(ui.soulPanel.activeSelf, Is.False);
            Assert.That(ui.noticeText.text, Is.EqualTo(InterfaceTexts.WakeHint));
            Assert.That(ui.titlePanel.activeSelf, Is.False);
            Assert.That(ui.endingPanel.gameObject.activeSelf, Is.False);
            Assert.That(ui.noticeText.gameObject.activeSelf, Is.True);
            Assert.That(ui.subtitleText.gameObject.activeSelf, Is.True);
            Assert.That((ui.IsFrozen, Time.timeScale, AudioListener.pause), Is.EqualTo((false, 1f, false)));
            ui.StartGame();
            Assert.That(ui.IsFrozen, Is.False);
            light.GetComponent<PlayerSoul>().Powers.Unlock(Power.Light);
            Assert.IsFalse(light.State.IsOn || GameInput.LightTogglePressed);
            for (int screen = 0; screen < 4; screen++)
            {
                InputState.Change(mouse.leftButton, 0f);
                if (screen % 2 == 0) ui.TogglePause(); else ui.ToggleNotebook();
                yield return null;
                InputState.Change(mouse.leftButton, 1f);
                if (screen < 2) ui.SendMessage("Update");
                if (screen % 2 == 0) ui.TogglePause(); else ui.ToggleNotebook();
                light.SendMessage("Update");
                Assert.IsFalse(light.State.IsOn || GameInput.LightTogglePressed);
                yield return null;
                Assert.IsFalse(light.State.IsOn || GameInput.LightTogglePressed);
            }
            InputState.Change(mouse.leftButton, 0f);
            yield return null;
            InputState.Change(mouse.leftButton, 1f);
            light.SendMessage("Update");
            Assert.IsTrue(light.State.IsOn);
        }
    }
}
