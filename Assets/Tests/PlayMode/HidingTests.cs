using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class HidingTests
    {
        GameObject world;
        int steps;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordStep;
            GameInput.ClearOverrides();
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
            var hiding = player.AddComponent<PlayerHiding>();
            hiding.body = new Renderer[] { body };
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
            Physics.SyncTransforms();
            yield return null;
            Vector3 entry = player.transform.position;

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            hiding.Hide(volume);
            Assert.IsTrue(hiding.Hidden && collider.isTrigger && controller.enabled);
            Assert.IsFalse(movement.enabled || body.enabled);
            Assert.Less(Vector3.Distance(volume.HidePoint, player.transform.position), 0.001f);
            GameInput.MoveOverride = Vector2.up;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, steps);
            Assert.AreEqual(volume.HidePoint, player.transform.position);
            Assert.IsTrue(witch.playerHidden);

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            hiding.Leave();
            hiding.Leave();
            Assert.IsFalse(hiding.Hidden);
            Assert.IsTrue(movement.enabled && body.enabled && controller.enabled);
            Assert.AreEqual(entry, player.transform.position);
            yield return new WaitForSeconds(1f);
            Assert.Greater(player.transform.position.z, entry.z + 1f);
            Assert.Greater(steps, 0);
            Assert.IsFalse(witch.playerHidden);
            LogAssert.NoUnexpectedReceived();
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
