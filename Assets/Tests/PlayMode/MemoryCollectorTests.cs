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
            Assert.IsNull(ball.echoPlayer);
            var cemetery = GameObject.Find("Route").transform.GetChild(0);
            Assert.AreSame(cemetery.Find("Ball").gameObject, ball.visual);
            Assert.AreEqual(ball.visual.transform.position, ball.transform.position);
            Assert.IsNull(ball.visual.GetComponent<Collider>());
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
            Assert.IsNull(collector.ActiveEcho);
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
                Assert.AreSame(collector.spots[1], stage.spot);
                Assert.AreEqual(name == "Ball 1 Tyler", stage.collect);
                Assert.Less(Vector3.Distance(stage.transform.position, stage.spot.transform.position), 2f);
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
            CollectionAssert.AreEqual(new[] { 1, MemoryProgress.BallId, 2, 3, 4, 5 },
                System.Array.ConvertAll(collector.spots, spot => spot.memoryId));
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
            Assert.AreEqual(6, collector.spots.Length);
            Assert.AreSame(collector.spots[0].echoPlayer, collector.ActiveEcho);
            Assert.IsTrue(collector.ActiveEcho.IsPlaying);
            Assert.IsTrue(collector.ActiveEcho.stageRoot.activeSelf);
            Assert.IsFalse(ui.noticeText.gameObject.activeSelf);
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
            Assert.AreSame(collector.spots[0].echoPlayer, collector.ActiveEcho);
            Assert.IsTrue(collector.ActiveEcho.IsPlaying);
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
            Assert.AreSame(collector.spots[3].transform, collector.tyler.nextMemory);
            Assert.IsFalse(collector.spots[0].echoPlayer.IsPlaying);
            Assert.AreSame(collector.spots[2].echoPlayer, collector.ActiveEcho);
#if UNITY_EDITOR
            Assert.AreEqual(InterfaceTexts.PowerNotice(Power.Candles), ui.noticeText.text);
            Assert.IsEmpty(ui.subtitleText.text);
            ui.ToggleNotebook();
            Assert.AreEqual("Listón", ui.notebookTitle.text);
            Assert.IsFalse(ui.notebookEmpty.gameObject.activeSelf);
            ui.ToggleNotebook();
#endif
            for (int id = 3; id <= 5; id++)
            {
                Assert.IsTrue(collector.Collect(id));
                Assert.IsTrue(collector.soul.Powers.Has(PowerSet.ForMemory(id)));
                Assert.IsFalse(collector.spots[id].visual.activeSelf);
                if (id < 5) Assert.AreSame(collector.spots[id + 1].transform, collector.tyler.nextMemory);
            }
            Assert.IsNull(collector.tyler.nextMemory);
            collector.enabled = false;
            Assert.AreEqual(0f, collector.echoVolume.weight);
            Assert.IsFalse(cameraData.renderPostProcessing);
            Assert.IsFalse(collector.spots[2].echoPlayer.stageRoot.activeSelf);
            Assert.IsTrue(ui.noticeText.gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator EchoCapturesRestoreTheWorldWithoutCollectingMemories()
        {
            var stages = Object.FindObjectsByType<EchoCaptureStage>(FindObjectsSortMode.None);
            Assert.AreEqual(5, stages.Length);
            var route = GameObject.Find("Route").transform;
            var echoes = route.parent.Find("Memory Echoes");
            Assert.IsNotNull(echoes);
            Assert.IsFalse(echoes.IsChildOf(route));
            var player = collector.GetComponent<PlayerController>();
            player.enabled = false;
            var follow = collector.viewCamera.GetComponent<FollowCamera>();
            follow.enabled = false;
            var position = collector.transform.position;
            var rotation = collector.transform.rotation;
            var cameraPosition = collector.viewCamera.transform.position;
            var cameraRotation = collector.viewCamera.transform.rotation;
            var nextMemory = collector.tyler.nextMemory;
            var cameraData = collector.viewCamera.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            collector.echoVolume.weight = 0.2f;
            collector.gameInterface.ReplaceSubtitle("Subtítulo anterior", 20f);
            foreach (var stage in stages)
            {
                var clearing = route.GetChild(stage.spot.memoryId - 1);
                Assert.AreSame(System.Array.Find(collector.spots, spot => spot.memoryId == stage.spot.memoryId), stage.spot);
                Assert.AreEqual(MemorySpotKind.Memory, stage.spot.kind);
                Assert.IsTrue(stage.spot.transform.IsChildOf(clearing));
                Assert.AreSame(echoes, stage.spot.echoPlayer.stageRoot.transform.parent);
                Assert.IsNotNull(stage.spot.echoPlayer.ground);
                Assert.IsTrue(stage.spot.echoPlayer.ground.transform.IsChildOf(clearing));
                Vector3 offset = stage.spot.transform.InverseTransformPoint(stage.transform.position);
                Assert.AreEqual(-3f, offset.x, 0.001f);
                Assert.AreEqual(-4f, offset.z, 0.001f);
                Assert.AreEqual(stage.spot.memoryId, stage.spot.echoPlayer.Data.id);
                Assert.AreEqual(stage.spot.echoPlayer.Data.duration * 0.5f, stage.GetComponent<CaptureStop>().wait);
                foreach (var source in stage.spot.echoPlayer.sources)
                {
                    Assert.AreEqual(0f, source.spatialBlend);
                    Assert.IsFalse(source.playOnAwake);
                    Assert.IsFalse(source.loop);
                }
                var power = PowerSet.ForMemory(stage.spot.memoryId);
                bool hadPower = collector.soul.Powers.Has(power);
                Assert.IsTrue(stage.Apply());
                Assert.AreEqual(stage.transform.position, collector.transform.position);
                Assert.AreEqual(stage.GetComponent<CaptureStop>().cameraPosition, collector.viewCamera.transform.position);
                yield return new WaitForSeconds(0.1f);
                Assert.IsTrue(stage.spot.echoPlayer.IsPlaying);
                Assert.IsFalse(stage.spot.visual.activeSelf);
                stage.Clear();
                Assert.IsFalse(stage.spot.echoPlayer.stageRoot.activeSelf);
                Assert.IsFalse(stage.spot.echoPlayer.blackout.gameObject.activeSelf);
                foreach (var source in stage.spot.echoPlayer.sources) Assert.IsFalse(source.isPlaying);
                Assert.IsTrue(stage.spot.visual.activeSelf);
                Assert.AreEqual(hadPower, collector.soul.Powers.Has(power));
                Assert.AreSame(nextMemory, collector.tyler.nextMemory);
                Assert.AreEqual(position, collector.transform.position);
                Assert.Less(Quaternion.Angle(rotation, collector.transform.rotation), 0.001f);
                Assert.AreEqual(cameraPosition, collector.viewCamera.transform.position);
                Assert.Less(Quaternion.Angle(cameraRotation, collector.viewCamera.transform.rotation), 0.001f);
                Assert.IsFalse(player.enabled);
                Assert.IsFalse(follow.enabled);
                Assert.IsFalse(collector.tyler.gameObject.activeSelf);
                Assert.IsFalse(collector.tyler.witch.gameObject.activeSelf);
                Assert.IsTrue(cameraData.renderPostProcessing);
                Assert.AreEqual(0.2f, collector.echoVolume.weight);
                Assert.AreEqual("Subtítulo anterior", collector.gameInterface.subtitleText.text);
            }
            Assert.IsTrue(collector.Collect(1));
        }
    }
}
