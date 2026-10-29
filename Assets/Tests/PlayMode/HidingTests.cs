using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class HidingTests : TiffanyLogTests
    {
        GameObject world;
        int steps;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordStep;
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Object.Destroy(world);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HidingStopsMovementAndNoiseUntilLeaving()
        {
            world = new GameObject("HidingTestWorld");
            var ground = Create("Ground");
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.AddComponent<BoxCollider>();
            var camera = Create("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 20f, -20f);
            camera.transform.LookAt(Vector3.zero);
            var player = Create("Player");
            player.transform.position = new Vector3(0f, 0.05f, -0.7f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = Vector3.up * 0.6f;
            var movement = player.AddComponent<PlayerController>();
            movement.viewCamera = camera;
            var body = Create("Body").AddComponent<MeshRenderer>();
            body.transform.SetParent(player.transform, false);
#if UNITY_EDITOR
            var model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Models/tiffany-provisional.fbx"), player.transform, false);
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Models/TiffanyProvisional.controller");
            player.AddComponent<TiffanyAnimator>().animator = animator;
#endif
            var rain = Create("Rain").AddComponent<ParticleSystem>();
            rain.transform.SetParent(player.transform, false);
            var rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
            var hiding = player.AddComponent<PlayerHiding>();
            hiding.body = PlayerHiding.BodyRenderers(player);
            var bush = Create("Hide Test");
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            var volume = bush.AddComponent<HideVolume>();
            var witchObject = Create("Witch");
            witchObject.SetActive(false);
            witchObject.AddComponent<NavMeshAgent>().enabled = false;
            var witch = witchObject.AddComponent<WitchController>();
            witch.enabled = false;
            witchObject.SetActive(true);
            NoiseBus.Published += RecordStep;
            steps = 0;
            GameInput.MoveOverride = Vector2.zero;
            GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = false;
            GameInput.SlowOverride = false;
            GameInput.InteractOverride = false;
            Physics.SyncTransforms();
            yield return null;
            GameInput.MoveOverride = Vector2.down;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(steps, 0);
            Vector3 entry = player.transform.position;
            steps = 0;

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            hiding.Hide(volume);
            Assert.IsTrue(hiding.Hidden && collider.isTrigger && controller.enabled);
            Assert.IsFalse(movement.enabled || body.enabled);
            Assert.IsTrue(rainRenderer.enabled);
            Assert.Less(Vector3.Distance(volume.HidePoint, player.transform.position), 0.001f);
            GameInput.MoveOverride = Vector2.up;
            movement.PublishFootstep();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, steps);
            Assert.AreEqual(volume.HidePoint, player.transform.position);
            Assert.IsTrue(witch.playerHidden);

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            hiding.Leave();
            hiding.Leave();
            Assert.IsFalse(hiding.Hidden);
            Assert.IsTrue(movement.enabled && body.enabled && controller.enabled);
            Assert.IsTrue(rainRenderer.enabled);
            Assert.AreEqual(entry, player.transform.position);
            yield return new WaitForSeconds(1f);
            Assert.Greater(player.transform.position.z, entry.z + 1f);
            Assert.Greater(steps, 0);
            Assert.IsFalse(witch.playerHidden);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InteractionFindsIgnoreRaycastHideVolumeOnlyWhenNearby()
        {
            var hiding = CreateInteractionWorld(out _);
            yield return null;

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsTrue(hiding.Hidden);
            yield return null;
            Assert.IsTrue(hiding.Hidden);

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(hiding.Hidden);
            yield return null;
            Assert.IsFalse(hiding.Hidden);

            var controller = hiding.GetComponent<CharacterController>();
            controller.enabled = false;
            hiding.transform.position += Vector3.right * 10f;
            controller.enabled = true;
            Physics.SyncTransforms();
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.IsFalse(hiding.Hidden);
            yield return null;
            Assert.IsFalse(hiding.Hidden);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PauseAndNotebookBlockEnteringAndLeaving()
        {
            var hiding = CreateInteractionWorld(out var volume);
            var gameInterface = CreateInterface();
            hiding.gameInterface = gameInterface;
            yield return null;

            foreach (var screen in new[] { InterfaceScreen.Paused, InterfaceScreen.Notebook })
            {
                if (screen == InterfaceScreen.Paused) gameInterface.TogglePause();
                else gameInterface.ToggleNotebook();
                Assert.IsTrue(gameInterface.IsFrozen);
                GameInput.InteractOverride = true;
                yield return null;
                GameInput.InteractOverride = false;
                Assert.IsFalse(hiding.Hidden);
                yield return null;

                gameInterface.TogglePause();
                Assert.IsFalse(gameInterface.IsFrozen);
                LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
                hiding.Hide(volume);
                if (screen == InterfaceScreen.Paused) gameInterface.TogglePause();
                else gameInterface.ToggleNotebook();
                Assert.IsTrue(gameInterface.IsFrozen);
                GameInput.InteractOverride = true;
                yield return null;
                GameInput.InteractOverride = false;
                Assert.IsTrue(hiding.Hidden);
                yield return null;

                gameInterface.TogglePause();
                Assert.IsFalse(gameInterface.IsFrozen);
                LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
                hiding.Leave();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LeavingClearsHorizontalAndVerticalVelocity()
        {
            var hiding = CreateInteractionWorld(out var volume);
            hiding.transform.position += Vector3.up * 3f;
            var movement = hiding.GetComponent<PlayerController>();
            movement.gravity = 20f;
            GameInput.MoveOverride = Vector2.up;
            Vector3 start = hiding.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(hiding.transform.position.z, start.z);
            Assert.Less(hiding.transform.position.y, start.y);

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            GameInput.MoveOverride = Vector2.zero;
            movement.gravity = 0f;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            hiding.Leave();
            Vector3 entry = hiding.transform.position;
            yield return new WaitForSeconds(0.1f);
            Assert.Less(Vector3.Distance(entry, hiding.transform.position), 0.001f);
            LogAssert.NoUnexpectedReceived();
        }

        PlayerHiding CreateInteractionWorld(out HideVolume volume)
        {
            world = new GameObject("HidingInteractionTestWorld");
            GameInput.MoveOverride = Vector2.zero;
            GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = false;
            GameInput.SlowOverride = false;
            GameInput.InteractOverride = false;
            var player = Create("Player");
            player.transform.position = new Vector3(0f, 0f, -1.2f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = Vector3.up * 0.6f;
            player.AddComponent<PlayerController>().gravity = 0f;
            var hiding = player.AddComponent<PlayerHiding>();
            var bush = Create("Hide Test");
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            volume = bush.AddComponent<HideVolume>();
            Physics.SyncTransforms();
            return hiding;
        }

        GameInterface CreateInterface()
        {
            var root = Create("Interface");
            root.SetActive(false);
            var gameInterface = root.AddComponent<GameInterface>();
            gameInterface.pausePanel = Create("Pause");
            gameInterface.notebookPanel = Create("Notebook");
            gameInterface.drawingImage = Create("Drawing").AddComponent<RawImage>();
            gameInterface.drawingFitter = gameInterface.drawingImage.gameObject.AddComponent<AspectRatioFitter>();
            gameInterface.notebookTitle = Create("Title").AddComponent<Text>();
            gameInterface.notebookPhrase = Create("Phrase").AddComponent<Text>();
            gameInterface.notebookPage = Create("Page").AddComponent<Text>();
            gameInterface.notebookEmpty = Create("Empty").AddComponent<Text>();
            gameInterface.noticeText = Create("Notice").AddComponent<Text>();
            gameInterface.subtitleText = Create("Subtitle").AddComponent<Text>();
            root.SetActive(true);
            return gameInterface;
        }

        GameObject Create(string name)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(world.transform, false);
            return instance;
        }

        void RecordStep(StepNoise noise) => steps++;
    }
}
