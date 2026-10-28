using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class VanishPowerTests
    {
        private GameObject world;
        private NavMeshSurface surface;
        private GameObject player;
        private PlayerSoul soul;
        private PlayerLight playerLight;
        private Light spot;
        private Transform glow;
        private Renderer figure;
        private PlayerHiding hiding;
        private VanishPower vanish;
        private WitchController witch;
        private NavMeshAgent agent;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            world = new GameObject("VanishTestWorld");
            var ground = new GameObject("Ground");
            ground.transform.SetParent(world.transform);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            ground.AddComponent<BoxCollider>();
            surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();

            player = new GameObject("Player") { tag = "Player" };
            player.SetActive(false);
            player.transform.SetParent(world.transform);
            soul = player.AddComponent<PlayerSoul>();
            soul.startingPowers = new[] { Power.Light, Power.Vanish };
            spot = new GameObject("Soul Light").AddComponent<Light>();
            spot.transform.SetParent(player.transform, false);
            spot.transform.localPosition = new Vector3(0f, 2.4f, -0.3f);
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.innerSpotAngle = 40f;
            spot.range = 11f;
            spot.intensity = 18f;
            glow = new GameObject("Hand Glow").transform;
            glow.SetParent(player.transform, false);
            glow.localScale = Vector3.one * 0.11f;
            playerLight = player.AddComponent<PlayerLight>();
            playerLight.spot = spot;
            playerLight.glow = glow;
            playerLight.startOn = true;
            var body = new GameObject("Body");
            body.transform.SetParent(player.transform, false);
            figure = body.AddComponent<MeshRenderer>();
            player.AddComponent<PlayerController>().enabled = false;
            hiding = player.AddComponent<PlayerHiding>();
            hiding.body = new[] { figure };
            vanish = player.AddComponent<VanishPower>();
            vanish.figure = new[] { figure };
            vanish.playerLight = playerLight;

            var witchObject = new GameObject("Witch");
            witchObject.SetActive(false);
            witchObject.transform.SetParent(world.transform);
            witchObject.transform.position = new Vector3(0f, 14f, 0f);
            agent = witchObject.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            witch = witchObject.AddComponent<WitchController>();
            witch.player = player.transform;
            witch.skyRoute = new[] { new Vector3(30f, 14f, 0f), new Vector3(30f, 14f, 30f) };
            playerLight.witch = witch;
            vanish.witch = witch;
            witchObject.SetActive(true);
            player.SetActive(true);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeZones.Clear();
            if (surface != null)
            {
                surface.RemoveData();
                Object.Destroy(surface.navMeshData);
            }
            Object.Destroy(world);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VanishSpendsEnergyHidesFigureAndLightForThreeSeconds()
        {
            yield return null;
            soul.Energy.ResetTo(SoulEnergy.Max);
            Assert.IsTrue(spot.enabled);
            Assert.IsTrue(vanish.TryStart());
            Assert.AreEqual(70f, soul.Energy.Current);
            Assert.IsTrue(witch.playerVanished);

            yield return new WaitForSeconds(1.5f);

            Assert.IsFalse(figure.enabled);
            Assert.AreEqual(0f, playerLight.visibility);
            Assert.IsFalse(spot.enabled);
            Assert.AreEqual(0f, spot.intensity);
            Assert.IsFalse(glow.gameObject.activeSelf);
            Assert.AreEqual(Vector3.zero, glow.localScale);
            Assert.IsTrue(witch.playerVanished);
            Assert.IsFalse(vanish.TryStart());

            yield return new WaitForSeconds(1.7f);

            Assert.IsFalse(vanish.State.IsActive);
            Assert.IsFalse(witch.playerVanished);
            Assert.IsTrue(figure.enabled);
            Assert.AreEqual(1f, playerLight.visibility);
            Assert.IsTrue(spot.enabled);
            Assert.AreEqual(18f, spot.intensity);
            Assert.IsTrue(glow.gameObject.activeSelf);
            Assert.AreEqual(Vector3.one * 0.11f, glow.localScale);
        }

        [UnityTest]
        public IEnumerator VanishDoesNotRevealTheHiddenGirl()
        {
            yield return null;
            var bush = new GameObject("Hide Test");
            bush.transform.SetParent(world.transform, false);
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            var volume = bush.AddComponent<HideVolume>();
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            Assert.IsTrue(hiding.Hidden);
            Assert.IsFalse(figure.enabled);
            Assert.IsTrue(vanish.isActiveAndEnabled);
            Assert.IsFalse(vanish.State.IsActive);

            float deadline = Time.time + 0.5f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.IsFalse(figure.enabled);
            }

            Assert.IsTrue(vanish.TryStart());
            yield return new WaitForSeconds(3.2f);

            Assert.IsFalse(vanish.State.IsActive);
            Assert.IsTrue(hiding.Hidden);
            Assert.IsFalse(figure.enabled);
        }

        [UnityTest]
        public IEnumerator LeavingHideoutWhileVanishedKeepsTheFigureHidden()
        {
            yield return null;
            var bush = new GameObject("Hide Test");
            bush.transform.SetParent(world.transform, false);
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            var volume = bush.AddComponent<HideVolume>();
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            Assert.IsTrue(hiding.Hidden);
            Assert.IsFalse(figure.enabled);
            Assert.IsTrue(vanish.TryStart());
            float deadline = Time.time + 3.2f;

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(vanish.State.IsActive);
            Assert.IsFalse(figure.enabled);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            hiding.Leave();
            yield return null;
            yield return null;

            Assert.IsFalse(hiding.Hidden);
            Assert.IsTrue(vanish.State.IsActive);
            Assert.IsFalse(figure.enabled);

            while (Time.time < deadline) yield return null;

            Assert.IsFalse(vanish.State.IsActive);
            Assert.IsTrue(figure.enabled);
        }

        [UnityTest]
        public IEnumerator GroundedWitchLosesTheGirlWhileVanished()
        {
            player.transform.position = new Vector3(20f, 0f, 0f);
            witch.playerLightOn = true;
            witch.playerLightPosition = player.transform.position;
            Physics.SyncTransforms();
            float deadline = Time.time + 6f;
            bool sawLanding = false;
            while (Time.time < deadline && witch.Decision.State != WitchState.Stalking)
            {
                sawLanding |= witch.Decision.State == WitchState.Landing;
                yield return null;
            }
            Assert.IsTrue(sawLanding);
            Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);

            playerLight.Toggle();
            Vector3 position = witch.transform.position + Vector3.back * 2f;
            position.y = 0f;
            player.transform.position = position;
            Physics.SyncTransforms();
            deadline = Time.time + 3f;
            bool detected = false;
            while (Time.time < deadline)
            {
                yield return null;
                detected = witch.Decision.State == WitchState.WindUp
                    || (witch.Decision.State == WitchState.Stalking
                        && Vector3.Distance(witch.Decision.MoveTarget, player.transform.position) < 1.5f);
                if (detected) break;
            }
            Assert.IsTrue(detected);
            WitchState detectedState = witch.Decision.State;

            // La detección a 2 m inicia WindUp antes de la primera embestida.
            Assert.IsTrue(vanish.TryStart());
            var states = new HashSet<WitchState>();
            deadline = Time.time + 2.5f;
            while (Time.time < deadline)
            {
                yield return null;
                WitchState state = witch.Decision.State;
                states.Add(state);
                Assert.IsTrue(witch.playerVanished);
                Assert.AreNotEqual(WitchState.WindUp, state);
                Assert.AreNotEqual(WitchState.Sprinting, state);
                Assert.AreNotEqual(WitchState.Caught, state);
            }

            Debug.Log($"{GameInfo.LogPrefix} Desvanecerse: detección {detectedState}, estados durante el poder: {string.Join(", ", states)}");
            Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
            Assert.Greater(Vector3.Distance(witch.Decision.MoveTarget, player.transform.position), 1.5f);
            Vector3 separation = player.transform.position - witch.transform.position;
            separation.y = 0f;
            Assert.AreEqual(2f, separation.magnitude, 0.1f);
        }
    }
}
