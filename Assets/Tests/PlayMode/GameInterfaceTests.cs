using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class GameInterfaceTests
    {
        GameInterface gameInterface;
        [UnitySetUp] public IEnumerator SetUp()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            gameInterface = Object.FindAnyObjectByType<GameInterface>();
#if UNITY_EDITOR
            const string folder = "Assets/Tests/Fixtures/Memories";
            gameInterface.LoadMemories(LoadAssets<TextAsset>("t:TextAsset"), LoadAssets<Texture2D>("t:Texture2D"));
            T[] LoadAssets<T>(string filter) where T : Object => System.Array.ConvertAll(
                UnityEditor.AssetDatabase.FindAssets(filter, new[] { folder }),
                guid => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
#endif
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(GameInterfaceTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }
        [UnityTest] public IEnumerator PauseAndEmptyNotebookRestoreTimeAndAudio()
        {
            gameInterface.TogglePause();
            AssertScreen(InterfaceScreen.Paused);
            Canvas.ForceUpdateCanvases();
            var controls = gameInterface.pauseControls;
            Assert.AreEqual(InterfaceTexts.ControllerText(InterfaceTexts.Controls, GameInterface.HasControllerSymbols(controls.font)), controls.text);
            var settings = controls.GetGenerationSettings(controls.rectTransform.rect.size);
            Assert.LessOrEqual(controls.cachedTextGenerator.GetPreferredHeight(controls.text, settings),
                controls.rectTransform.rect.height * controls.pixelsPerUnit);
            yield return null;
            gameInterface.ToggleNotebook();
            AssertScreen(InterfaceScreen.Paused);
            gameInterface.enabled = false;
            AssertFrozen(false);
            gameInterface.enabled = true;
            AssertFrozen(true);
            gameInterface.TogglePause();
            AssertScreen(InterfaceScreen.Playing);
            gameInterface.ToggleNotebook();
            Assert.That((gameInterface.notebookEmpty.gameObject.activeInHierarchy,
                gameInterface.drawingImage.gameObject.activeInHierarchy), Is.EqualTo((true, false)));
            gameInterface.TogglePause();
            AssertScreen(InterfaceScreen.Playing);
        }
        [UnityTest] public IEnumerator NotebookHidesLinesFreezesTimersAndRefreshesPages()
        {
            Assert.That((gameInterface.memoryAssets.Length, gameInterface.drawings.Length), Is.EqualTo((2, 4)));
            foreach (var drawing in gameInterface.drawings)
                Assert.That((drawing.width, drawing.height), Is.EqualTo((640, 480)));
            Assert.That(gameInterface.MemoryFound(99), Is.False);
            Assert.That(gameInterface.MemoryFound(2), Is.True);
            Assert.That(gameInterface.MemoryFound(2), Is.False);
            Assert.That(gameInterface.noticeText.text, Is.EqualTo(InterfaceTexts.PowerNotice(Power.Candles)));
            gameInterface.ShowSubtitle("Subtítulo de prueba", 0.5f);
            AssertScreen(InterfaceScreen.Playing);
            gameInterface.ToggleNotebook();
            AssertScreen(InterfaceScreen.Notebook);
            gameInterface.NextPage();
            AssertPage("Listón", "2 / 2", "memory-test-2-2", "Dibujo de prueba 4");
            gameInterface.MemoryFound(1);
            AssertPage("Flores", "1 / 4", "memory-test-1-1", "Dibujo de prueba 1");
            Assert.That(gameInterface.drawingFitter.aspectRatio, Is.EqualTo(4f / 3f).Within(0.001f));
            gameInterface.NextPage();
            AssertPage("Flores", "2 / 4", "memory-test-1-2", "Dibujo de prueba 2");
            gameInterface.PreviousPage();
            AssertPage("Flores", "1 / 4", "memory-test-1-1", "Dibujo de prueba 1");
            yield return new WaitForSecondsRealtime(0.7f);
            AssertScreen(InterfaceScreen.Notebook);
            Assert.That(gameInterface.subtitleText.text, Is.EqualTo("Subtítulo de prueba"));
            gameInterface.ToggleNotebook();
            AssertScreen(InterfaceScreen.Playing);
            yield return new WaitForSeconds(0.7f);
            Assert.That(gameInterface.subtitleText.text, Is.Empty);
        }
        [Test] public void EmptyMemoriesRefreshOpenNotebook()
        {
            Assert.That(gameInterface.MemoryFound(1), Is.True);
            gameInterface.ToggleNotebook();
            Assert.That(gameInterface.drawingImage.gameObject.activeInHierarchy, Is.True);
            gameInterface.LoadMemories(System.Array.Empty<TextAsset>(), System.Array.Empty<Texture2D>());
            Assert.That((gameInterface.notebookEmpty.gameObject.activeInHierarchy, gameInterface.notebookEmpty.text,
                gameInterface.drawingImage.gameObject.activeInHierarchy), Is.EqualTo((true, InterfaceTexts.NotebookEmpty, false)));
            Assert.That((gameInterface.MemoryFound(1), gameInterface.noticeText.text), Is.EqualTo((false, string.Empty)));
        }
        [Test] public void SoulBarReadsPlayerEnergyAndFollowsInterfaceVisibility()
        {
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
            gameInterface.MemoryFound(2);
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
            gameInterface.MemoryFound(1);
            Assert.That(gameInterface.soulPanel.activeSelf, Is.True);
            var energy = gameInterface.soul.Energy;
            energy.ResetTo(80f);
            gameInterface.SendMessage("Update");
            Assert.That(gameInterface.soulFill.rectTransform.anchorMax.x, Is.EqualTo(0.8f).Within(0.001f));
            energy.TrySpend(30f);
            gameInterface.SendMessage("Update");
            Assert.That(gameInterface.soulFill.rectTransform.anchorMax.x, Is.EqualTo(0.5f));
            var spentColor = gameInterface.soulFill.color;
            energy.Recharge(1f);
            gameInterface.SendMessage("Update");
            Assert.That(gameInterface.soulFill.rectTransform.anchorMax.x, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(gameInterface.soulFill.color, Is.Not.EqualTo(spentColor));
            energy.ResetTo(10f);
            gameInterface.SendMessage("Update");
            Assert.That(gameInterface.soulFill.color, Is.Not.EqualTo(spentColor));
            gameInterface.TogglePause();
            Assert.That(gameInterface.soulPanel.activeSelf, Is.True);
            gameInterface.TogglePause();
            gameInterface.ToggleNotebook();
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
            gameInterface.ToggleNotebook();
            Assert.That(gameInterface.soulPanel.activeSelf, Is.True);
            var ending = new EndingRules();
            Assert.That(ending.TryManifest(true, 0f, true), Is.True);
            gameInterface.ShowEnding(ending);
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
            ending.Tick(EndingRules.FadeSeconds + EndingRules.TextSeconds);
            gameInterface.ShowEnding(ending);
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
        }
        [Test] public void RestoringFirstMemoryUnlocksSoulBarEvenWithoutNotebookAssetsOrNotice()
        {
            gameInterface.LoadMemories(System.Array.Empty<TextAsset>(), System.Array.Empty<Texture2D>());
            Assert.That(gameInterface.soulPanel.activeSelf, Is.False);
            Assert.That(gameInterface.MemoryFound(1, false), Is.False);
            Assert.That(gameInterface.soulPanel.activeSelf, Is.True);
            Assert.That(gameInterface.noticeText.text, Is.Empty);
        }

        [Test] public void ObjectiveStartsVisibleAndRefreshesRestoredProgressWithoutCollectionEvents()
        {
            var hud = Object.FindAnyObjectByType<GameplayHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.objectiveText.gameObject.activeInHierarchy, Is.True);
            Assert.That(hud.objectiveText.text, Is.EqualTo(InterfaceTexts.GraveObjective));
            Assert.That(hud.objectiveText.font, Is.SameAs(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")));
            var progress = gameInterface.memories.Progress;
            progress.MarkFound(1);
            hud.SendMessage("LateUpdate");
            Assert.That(hud.objectiveText.text, Is.EqualTo(InterfaceTexts.BallObjective));
            progress.MarkFound(MemoryProgress.BallId);
            hud.SendMessage("LateUpdate");
            Assert.That(hud.objectiveText.text, Is.EqualTo(InterfaceTexts.BirdsObjective));
            gameInterface.ToggleNotebook();
            hud.SendMessage("LateUpdate");
            Assert.That(hud.objectiveText.gameObject.activeSelf, Is.False);
            Assert.That(hud.interactionText.gameObject.activeSelf, Is.False);
            gameInterface.ToggleNotebook();
            hud.SendMessage("LateUpdate");
            Assert.That(hud.objectiveText.gameObject.activeInHierarchy, Is.True);
            Assert.That(hud.objectiveText.text, Is.EqualTo(InterfaceTexts.BirdsObjective));
        }
        void AssertScreen(InterfaceScreen screen)
        {
            bool frozen = screen != InterfaceScreen.Playing;
            bool notebook = screen == InterfaceScreen.Notebook;
            AssertFrozen(frozen);
            Assert.That((gameInterface.Screen, gameInterface.IsFrozen,
                gameInterface.pausePanel.activeInHierarchy, gameInterface.notebookPanel.activeInHierarchy,
                gameInterface.noticeText.gameObject.activeSelf, gameInterface.subtitleText.gameObject.activeSelf),
                Is.EqualTo((screen, frozen, screen == InterfaceScreen.Paused, notebook, !notebook, !notebook)));
        }
        void AssertFrozen(bool frozen) =>
            Assert.That((Time.timeScale, AudioListener.pause), Is.EqualTo((frozen ? 0f : 1f, frozen)));
        void AssertPage(string title, string number, string image, string phrase) =>
            Assert.That((gameInterface.notebookTitle.text, gameInterface.notebookPage.text,
                gameInterface.drawingImage.texture.name, gameInterface.notebookPhrase.text), Is.EqualTo((title, number, image, phrase)));
    }
}
