using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class TitleScreenTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInterface.SkipTitle = true;
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
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
            ui.StartGame();
            ui.SendMessage("Update");
            GameInput.FlashOverride = true;
            light.SendMessage("Update");
            GameInput.FlashOverride = false;
            Assert.That(light.State.IsFlashing, Is.False);
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
        }
    }
}
