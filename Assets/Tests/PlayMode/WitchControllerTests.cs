using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class WitchControllerTests
    {
        private GameObject world;
        private NavMeshSurface surface;
        private GameObject player;
        private GameObject witch;
        private NavMeshAgent agent;
        private WitchController controller;
        private Light fireLight;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            world = new GameObject("WitchTestWorld");
            var ground = new GameObject("Ground");
            ground.transform.SetParent(world.transform);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            ground.AddComponent<BoxCollider>();
            surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();

            player = new GameObject("Player") { tag = "Player" };
            player.transform.SetParent(world.transform);
            player.transform.position = new Vector3(-30f, 0f, -30f);
            var capsule = new GameObject("PlayerBody");
            capsule.transform.SetParent(player.transform, false);
            var collider = capsule.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.4f;
            collider.center = Vector3.up;

            witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(world.transform);
            witch.transform.position = new Vector3(0f, 14f, 0f);
            agent = witch.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            controller = witch.AddComponent<WitchController>();
            controller.player = player.transform;
            controller.skyRoute = new[] { new Vector3(30f, 14f, 0f), new Vector3(30f, 14f, 30f) };
            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(witch.transform, false);
            fireLight = lightObject.AddComponent<Light>();
            fireLight.enabled = false;
            controller.fireLight = fireLight;
            witch.SetActive(true);
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
        public IEnumerator FlyingFollowsSkyRouteWithFireAndWithoutAgent()
        {
            float deadline = Time.time + 1f;
            while (Time.time < deadline) yield return null;

            Assert.That(witch.transform.position.x, Is.InRange(4f, 7f));
            Assert.AreEqual(14f, witch.transform.position.y, 1e-4f);
            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
            Assert.IsFalse(agent.enabled);
            Assert.IsTrue(fireLight.enabled);
        }

        [UnityTest]
        public IEnumerator PlayerLightLeadsToLandingAndGroundedStalking()
        {
            TurnOnPlayerLight();

            yield return WaitForStalking();
        }

        [UnityTest]
        public IEnumerator LandingPushesWitchOutsideSafeZone()
        {
            SafeZones.Add(new Vector3(8f, 0f, 0f), 5f);
            TurnOnPlayerLight();
            Assert.IsFalse(SafeZones.Contains(player.transform.position));

            yield return WaitForStalking();

            Assert.IsFalse(SafeZones.Contains(witch.transform.position));
        }

        private void TurnOnPlayerLight()
        {
            player.transform.position = new Vector3(20f, 0f, 0f);
            controller.playerLightOn = true;
            controller.playerLightPosition = player.transform.position;
            Physics.SyncTransforms();
        }

        private IEnumerator WaitForStalking()
        {
            bool sawLanding = false;
            float deadline = Time.time + 6f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stalking)
            {
                sawLanding |= controller.Decision.State == WitchState.Landing;
                yield return null;
            }

            Assert.IsTrue(sawLanding, "La bruja debe pasar por aterrizaje.");
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsTrue(agent.enabled);
            Assert.IsTrue(agent.isOnNavMesh);
            Assert.Less(Mathf.Abs(witch.transform.position.y), 0.3f);
        }
    }
}
