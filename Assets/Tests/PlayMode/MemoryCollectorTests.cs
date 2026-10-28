using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace TiffanyGame.Tests
{
    public sealed class MemoryCollectorTests
    {
        MemoryCollector collector;
        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            collector = Object.FindAnyObjectByType<MemoryCollector>();
            Assert.IsNotNull(collector);
            collector.tyler.witch.gameObject.SetActive(false);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(MemoryCollectorTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }
        [UnityTest] public IEnumerator ReplacingSubtitleClearsPreviousTextAndQueue()
        {
            collector.gameInterface.ReplaceSubtitle("Primer eco", 10f);
            collector.gameInterface.ShowSubtitle("En cola", 10f);
            collector.gameInterface.ReplaceSubtitle("Segundo eco", 0.1f);
            Assert.AreEqual("Segundo eco", collector.gameInterface.subtitleText.text);
            yield return new WaitForSeconds(0.2f);
            Assert.IsEmpty(collector.gameInterface.subtitleText.text);
        }
        [UnityTest] public IEnumerator CollectsOnceUnlocksNotifiesGuidesAndInteractsWithNextSpot()
        {
            var ui = collector.gameInterface;
            ui.LoadMemories(System.Array.Empty<TextAsset>(), System.Array.Empty<Texture2D>());
            var cameraData = collector.viewCamera.GetComponent<UniversalAdditionalCameraData>();
            Assert.AreSame(collector.spots[0].transform, collector.tyler.nextMemory);
            Assert.IsFalse(collector.Collect(99));
            Assert.IsTrue(collector.Collect(1));
            Assert.IsFalse(collector.Collect(1));
            Assert.IsTrue(collector.soul.Powers.Has(Power.Light));
            Assert.AreEqual(InterfaceTexts.PowerNotice(Power.Light), ui.noticeText.text);
            Assert.AreSame(collector.spots[1].transform, collector.tyler.nextMemory);
            Assert.IsFalse(collector.spots[0].visual.activeSelf);
            yield return new WaitForSeconds(1f);
            Assert.Greater(collector.echoVolume.weight, 0f);
            Assert.IsTrue(cameraData.renderPostProcessing);
#if UNITY_EDITOR
            const string folder = "Assets/Tests/Fixtures/Memories/";
            ui.LoadMemories(new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "memory-2.json") },
                new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "memory-test-2-1.png") });
#endif
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = collector.spots[1].transform.position;
            character.enabled = true;
            Physics.SyncTransforms();
            ui.TogglePause();
            GameInput.InteractOverride = true;
            yield return null;
            Assert.IsTrue(collector.spots[1].visual.activeSelf);
            ui.TogglePause();
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(collector.Collect(2));
            Assert.IsTrue(collector.soul.Powers.Has(Power.Candles));
            Assert.IsFalse(collector.spots[1].visual.activeSelf);
            Assert.IsNull(collector.tyler.nextMemory);
#if UNITY_EDITOR
            Assert.AreEqual(InterfaceTexts.PowerNotice(Power.Candles), ui.noticeText.text);
            Assert.AreEqual("Listón · Jacal de la bruja", ui.subtitleText.text);
            ui.ToggleNotebook();
            Assert.AreEqual("Listón", ui.notebookTitle.text);
            Assert.IsFalse(ui.notebookEmpty.gameObject.activeSelf);
            ui.ToggleNotebook();
#endif
            collector.enabled = false;
            Assert.AreEqual(0f, collector.echoVolume.weight);
            Assert.IsFalse(cameraData.renderPostProcessing);
        }
    }
}
