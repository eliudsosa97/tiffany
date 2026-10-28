using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
        [UnityTest] public IEnumerator BallRevealsTylerOnceWithoutPowerNoticeNotebookOrEcho()
        {
            var tyler = collector.tyler;
            var ball = collector.spots[1];
            var ui = collector.gameInterface;
            ui.LoadMemories(System.Array.Empty<TextAsset>(), System.Array.Empty<Texture2D>());
            collector.soul = new GameObject("Ball Test Soul").AddComponent<PlayerSoul>();
            Assert.IsFalse(tyler.gameObject.activeSelf);
            tyler.transform.position = collector.transform.position;
            Assert.IsFalse(tyler.NearPlayer);
            Assert.AreEqual(MemorySpotKind.Ball, ball.kind);
            Assert.AreEqual(MemoryProgress.BallId, ball.memoryId);
            Assert.Greater(Vector3.Distance(ball.transform.position, collector.spots[0].transform.position), 4f);
            var bush = Object.FindAnyObjectByType<HideVolume>();
            Assert.Greater(Vector3.Distance(ball.transform.position, bush.transform.position), 4f);
            foreach (var collider in ball.GetComponentsInChildren<Collider>()) Assert.IsTrue(collider.isTrigger);
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = ball.transform.position;
            character.enabled = true;
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Tyler aparece");
            Assert.IsTrue(collector.Collect(MemoryProgress.BallId));
            Assert.IsTrue(tyler.isActiveAndEnabled);
            Assert.IsTrue(tyler.NearPlayer);
            Assert.IsTrue(tyler.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.That(Vector3.Distance(tyler.transform.position, collector.transform.position), Is.InRange(1.5f, 3f));
            Assert.IsFalse(ball.visual.activeSelf);
            Assert.AreSame(collector.spots[0].transform, tyler.nextMemory);
            Assert.IsNotNull(tyler.barkClip);
            Assert.AreEqual("bark_1", tyler.barkClip.name);
            Assert.AreEqual(0f, tyler.voice.spatialBlend);
            yield return null;
            Assert.IsTrue(tyler.voice.isPlaying);
            Assert.AreEqual(0, collector.soul.Powers.Count);
            Assert.IsEmpty(ui.noticeText.text);
            Assert.IsEmpty(ui.subtitleText.text);
            Assert.AreEqual(0f, collector.echoVolume.weight);
            Assert.IsFalse(collector.viewCamera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing);
            tyler.voice.Stop();
            Assert.IsFalse(collector.Collect(MemoryProgress.BallId));
            Assert.IsFalse(tyler.voice.isPlaying);
            ui.ToggleNotebook();
            Assert.IsTrue(ui.notebookEmpty.gameObject.activeSelf);
            ui.ToggleNotebook();
            Assert.IsTrue(collector.Collect(1));
            Assert.AreSame(collector.spots[2].transform, tyler.nextMemory);
        }
        [UnityTest] public IEnumerator BallCaptureStagesShowBeforeAndAfterWithLightAndFixedCamera()
        {
            var stages = Object.FindObjectsByType<MemoryCaptureStage>(FindObjectsSortMode.None);
            foreach (string name in new[] { "Ball 0 Before", "Ball 1 Tyler" })
            {
                var stage = System.Array.Find(stages, candidate => candidate.name == name);
                Assert.IsNotNull(stage);
                var stop = stage.GetComponent<CaptureStop>();
                Assert.IsTrue(stop.overrideCamera);
                var light = stage.GetComponent<LightCaptureStage>();
                Assert.IsNotNull(light);
                light.Apply(collector.transform);
                stage.Apply();
                yield return null;
                Assert.IsTrue(collector.GetComponent<PlayerLight>().State.IsOn);
                Assert.AreEqual(stage.collect, collector.tyler.gameObject.activeSelf);
                Assert.AreEqual(!stage.collect, stage.spot.visual.activeSelf);
                light.Clear();
                stage.Clear();
            }
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
            int powersBeforeBall = collector.soul.Powers.Count;
            float echoBeforeBall = collector.echoVolume.weight;
            Assert.IsTrue(collector.Collect(MemoryProgress.BallId));
            Assert.IsTrue(collector.tyler.NearPlayer);
            Assert.AreEqual(powersBeforeBall, collector.soul.Powers.Count);
            Assert.AreEqual(InterfaceTexts.PowerNotice(Power.Light), ui.noticeText.text);
            Assert.AreEqual(echoBeforeBall, collector.echoVolume.weight);
            Assert.AreSame(collector.spots[2].transform, collector.tyler.nextMemory);
#if UNITY_EDITOR
            const string folder = "Assets/Tests/Fixtures/Memories/";
            ui.LoadMemories(new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "memory-2.json") },
                new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "memory-test-2-1.png") });
#endif
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = collector.spots[2].transform.position;
            character.enabled = true;
            Physics.SyncTransforms();
            ui.TogglePause();
            GameInput.InteractOverride = true;
            yield return null;
            Assert.IsTrue(collector.spots[2].visual.activeSelf);
            ui.TogglePause();
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(collector.Collect(2));
            Assert.IsTrue(collector.soul.Powers.Has(Power.Candles));
            Assert.IsFalse(collector.spots[2].visual.activeSelf);
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
