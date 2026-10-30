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
#if UNITY_EDITOR
        [UnityTest] public IEnumerator DrawingsFreezeTheGameAndDelayThePowerNoticeUntilTheReturnFade()
        {
            var ui = collector.gameInterface;
            const string folder = "Assets/Tests/Fixtures/Memories/";
            ui.LoadMemories(new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "memory-2.json") },
                new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "memory-test-2-1.png"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "memory-test-2-2.png") });
            Assert.IsTrue(collector.Collect(2));
            Assert.AreEqual(InterfaceScreen.Memory, ui.Screen);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsEmpty(ui.noticeText.text);
            Assert.IsFalse(ui.drawingImage.gameObject.activeSelf);
            ui.TogglePause();
            ui.ToggleNotebook();
            Assert.AreEqual(InterfaceScreen.Memory, ui.Screen);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, ui.notebookBackground.color.a);
            Assert.AreEqual("Listón", ui.notebookTitle.text);
            Assert.AreEqual("Dibujo de prueba 3", ui.notebookPhrase.text);
            Assert.AreEqual("memory-test-2-1", ui.drawingImage.texture.name);
            Assert.AreEqual(InterfaceTexts.MemoryHint, ui.notebookHint.text);
            Assert.IsFalse(ui.subtitleText.gameObject.activeSelf);
            Assert.Greater(collector.ActiveEcho.Elapsed, 0f);
            Assert.AreEqual(0f, collector.echoVolume.weight);
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.AreEqual("memory-test-2-2", ui.drawingImage.texture.name);
            Assert.AreEqual("Dibujo de prueba 4", ui.notebookPhrase.text);
            yield return null;
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(ui.drawingImage.gameObject.activeSelf);
            Assert.IsEmpty(ui.noticeText.text);
            Assert.IsTrue(ui.IsFrozen);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.AreEqual(InterfaceTexts.ControllerText(InterfaceTexts.PowerNotice(Power.Candles),
                GameInterface.HasControllerSymbols(ui.noticeText.font)), ui.noticeText.text);
            Assert.IsFalse(collector.spots[2].echoPlayer.IsPlaying);
            ui.ToggleNotebook();
            Assert.AreEqual("memory-test-2-1", ui.drawingImage.texture.name);
            Assert.AreEqual(InterfaceTexts.NotebookHint, ui.notebookHint.text);
            ui.ToggleNotebook();
        }
#endif
        [UnityTest] public IEnumerator MemoryWithoutDrawingsNotifiesImmediatelyWithoutEcho()
        {
            var ui = collector.gameInterface;
            var asset = new TextAsset("{\"id\":1,\"title\":\"Prueba\",\"drawings\":[]}");
            ui.LoadMemories(new[] { asset }, System.Array.Empty<Texture2D>());
            Assert.IsTrue(collector.Collect(1));
            Object.Destroy(asset);
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
            Assert.IsNull(collector.ActiveEcho);
            Assert.IsFalse(collector.spots[0].echoPlayer.IsPlaying);
            Assert.IsNotEmpty(ui.noticeText.text);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }

        [UnityTest] public IEnumerator DisablingInterfaceDuringMemoryRestoresTimeAndStopsAudio()
        {
            var ui = collector.gameInterface;
            Assert.IsTrue(collector.Collect(1));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Memory, ui.Screen);
            ui.enabled = false;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(collector.spots[0].echoPlayer.IsPlaying);
            ui.enabled = true;
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
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
            Assert.AreEqual(InterfaceTexts.ControllerText(InterfaceTexts.PowerNotice(Power.Light),
                GameInterface.HasControllerSymbols(ui.noticeText.font)), ui.noticeText.text);
            Assert.AreSame(collector.spots[1].transform, collector.tyler.nextMemory);
            Assert.IsFalse(collector.spots[0].visual.activeSelf);
            Assert.AreEqual(6, collector.spots.Length);
            Assert.IsNull(collector.ActiveEcho);
            Assert.IsTrue(ui.noticeText.gameObject.activeSelf);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, collector.echoVolume.weight);
            Assert.IsFalse(cameraData.renderPostProcessing);
            int powersBeforeBall = collector.soul.Powers.Count;
            float echoBeforeBall = collector.echoVolume.weight;
            Assert.IsTrue(collector.Collect(MemoryProgress.BallId));
            Assert.IsTrue(collector.tyler.NearPlayer);
            Assert.AreEqual(powersBeforeBall, collector.soul.Powers.Count);
            Assert.AreEqual(InterfaceTexts.ControllerText(InterfaceTexts.PowerNotice(Power.Light),
                GameInterface.HasControllerSymbols(ui.noticeText.font)), ui.noticeText.text);
            Assert.AreEqual(echoBeforeBall, collector.echoVolume.weight);
            Assert.IsNull(collector.ActiveEcho);
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
#if UNITY_EDITOR
            Assert.AreSame(collector.spots[2].echoPlayer, collector.ActiveEcho);
            Assert.AreEqual(InterfaceScreen.Memory, ui.Screen);
            Assert.IsFalse(ui.noticeText.gameObject.activeSelf);
            collector.StopEcho();
            yield return null;
            Assert.IsEmpty(ui.subtitleText.text);
            ui.ToggleNotebook();
            Assert.AreEqual("Listón", ui.notebookTitle.text);
            Assert.IsFalse(ui.notebookEmpty.gameObject.activeSelf);
            ui.ToggleNotebook();
#endif
            foreach (var challenge in collector.challenges)
            {
                challenge.Rules.Tick(-1f, true, 3, true, true);
                challenge.Rules.Tick(ChallengeRules.PassageLength, true, 3, true, true);
            }
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
            Assert.IsFalse(collector.spots[2].echoPlayer.IsPlaying);
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
                Assert.IsEmpty(echoes.GetComponentsInChildren<Renderer>(true));
                Vector3 offset = stage.spot.transform.InverseTransformPoint(stage.transform.position);
                Assert.AreEqual(-3f, offset.x, 0.001f);
                Assert.AreEqual(-4f, offset.z, 0.001f);
                Assert.AreEqual(stage.spot.memoryId, stage.spot.echoPlayer.Data.id);
                Assert.AreEqual(stage.spot.echoPlayer.Data.duration * 0.5f, stage.GetComponent<CaptureStop>().wait);
                foreach (var source in stage.spot.echoPlayer.sources)
                {
                    Assert.AreSame(echoes, source.transform.parent.parent);
                    Assert.IsTrue(source.ignoreListenerPause);
                    Assert.AreEqual(0f, source.spatialBlend);
                    Assert.IsFalse(source.playOnAwake);
                    Assert.IsFalse(source.loop);
                }
                var power = PowerSet.ForMemory(stage.spot.memoryId);
                bool hadPower = collector.soul.Powers.Has(power);
                Assert.IsTrue(stage.Apply());
                Assert.AreEqual(stage.transform.position, collector.transform.position);
                Assert.AreEqual(stage.GetComponent<CaptureStop>().cameraPosition, collector.viewCamera.transform.position);
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.IsTrue(stage.spot.echoPlayer.IsPlaying);
                Assert.IsFalse(stage.spot.visual.activeSelf);
                stage.Clear();
                Assert.IsFalse(stage.spot.echoPlayer.IsPlaying);
                Assert.AreEqual(InterfaceScreen.Playing, collector.gameInterface.Screen);
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

        [TestCase(1)]
        [TestCase(MemoryProgress.BallId)]
        public void NearbyQueryUsesDistanceToCenterAndRejectsFoundMemories(int memoryId)
        {
            var spot = System.Array.Find(collector.spots, candidate => candidate.memoryId == memoryId);
            foreach (var other in collector.spots)
                if (other != spot) other.GetComponent<SphereCollider>().enabled = false;
            var sphere = spot.GetComponent<SphereCollider>();
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = center + Vector3.right * (collector.searchRadius + 0.05f);
            Physics.SyncTransforms();
            Assert.That(collector.FindNearby(out _), Is.Null);
            collector.transform.position -= Vector3.right * 0.1f;
            Physics.SyncTransforms();
            Assert.That(collector.FindNearby(out float distance), Is.SameAs(spot));
            Assert.That(distance, Is.EqualTo(Mathf.Pow(collector.searchRadius - 0.05f, 2f)).Within(0.001f));
            collector.Progress.MarkFound(memoryId);
            Assert.That(collector.FindNearby(out _), Is.Null);
            character.enabled = true;
        }

        [UnityTest] public IEnumerator InteractAtOnePointTwoMetresFromGraveShowsPromptAndCollectsMemory()
        {
            var memory = collector.spots[0];
            var hud = Object.FindAnyObjectByType<GameplayHud>();
            Assert.IsNotNull(hud);
            collector.GetComponent<PlayerController>().enabled = false;
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = memory.transform.position + Vector3.right * 1.55f;
            Physics.SyncTransforms();
            hud.SendMessage("LateUpdate");
            Assert.That(collector.FindNearby(out _), Is.Null);
            Assert.That(hud.interactionText.gameObject.activeSelf, Is.False);
            collector.transform.position = memory.transform.position + Vector3.right * 1.2f;
            Physics.SyncTransforms();
            Assert.That(collector.FindNearby(out float distance), Is.SameAs(memory));
            Assert.That(distance, Is.EqualTo(1.2f * 1.2f).Within(0.001f));
            hud.SendMessage("LateUpdate");
            Assert.That(hud.interactionText.gameObject.activeSelf, Is.True);
            Assert.That(hud.interactionText.text, Is.EqualTo("Espacio / Equis: recordar"));
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.That(collector.IsFound(1), Is.True);
            Assert.That(collector.IsFound(MemoryProgress.BallId), Is.False);
            Assert.That(memory.visual.activeSelf, Is.False);
            hud.SendMessage("LateUpdate");
            Assert.That(hud.interactionText.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator InteractChoosesCloserBallWhenPickupSpheresOverlap()
        {
            var memory = collector.spots[0];
            var ball = collector.spots[1];
            collector.GetComponent<PlayerController>().enabled = false;
            collector.GetComponent<CharacterController>().enabled = false;
            collector.transform.position = memory.transform.position + Vector3.right * 0.9f;
            ball.transform.position = collector.transform.position + Vector3.right * 0.3f;
            Physics.SyncTransforms();
            Assert.That(memory.GetComponent<SphereCollider>().ClosestPoint(collector.transform.position),
                Is.EqualTo(collector.transform.position));
            Assert.That(ball.GetComponent<SphereCollider>().ClosestPoint(collector.transform.position),
                Is.EqualTo(collector.transform.position));
            Assert.That(collector.FindNearby(out _), Is.SameAs(ball));
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.That(collector.IsFound(MemoryProgress.BallId), Is.True);
            Assert.That(collector.IsFound(1), Is.False);
            Assert.That(ball.visual.activeSelf, Is.False);
            Assert.That(memory.visual.activeSelf, Is.True);
            Assert.That(collector.FindNearby(out _), Is.SameAs(memory));
        }

        [UnityTest] public IEnumerator InteractChoosesCloserMemoryBeforeHideAndDoesNotCollectWhenLeaving()
        {
            var hide = Object.FindAnyObjectByType<HideVolume>();
            var hideCollider = hide.GetComponent<BoxCollider>();
            var memory = collector.spots[0];
            foreach (var other in Object.FindObjectsByType<HideVolume>(FindObjectsSortMode.None))
                if (other != hide) other.GetComponent<Collider>().enabled = false;
            foreach (var other in collector.spots)
                if (other != memory) other.GetComponent<SphereCollider>().enabled = false;
            var character = collector.GetComponent<CharacterController>();
            character.enabled = false;
            collector.transform.position = memory.transform.position;
            character.enabled = true;
            collector.GetComponent<PlayerController>().enabled = false;
            hideCollider.center = Vector3.zero;
            hideCollider.size = Vector3.one * 0.1f;
            hide.transform.position = collector.transform.position + Vector3.right * 0.3f;
            Physics.SyncTransforms();
            Assert.That(collector.FindNearby(out float memoryDistance), Is.SameAs(memory));
            Assert.That(collector.hiding.FindNearby(out float hideDistance), Is.Not.Null);
            Assert.That(memoryDistance, Is.LessThan(hideDistance));
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.That(collector.IsFound(1), Is.True);
            Assert.That(collector.hiding.Hidden, Is.False);
            yield return null;
            collector.Progress.Clear();
            collector.hiding.Hide(hide);
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.That(collector.hiding.Hidden, Is.False);
            Assert.That(collector.IsFound(1), Is.False);
        }
    }
}
