using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PoltergeistTests
    {
        private GameObject world;
        private NavMeshSurface surface;
        private GameObject player;
        private PlayerSoul soul;
        private Poltergeist poltergeist;
        private Light handLight;
        private GameObject witch;
        private NavMeshAgent agent;
        private WitchController controller;
        private StepNoise publishedNoise;
        private int publishedCount;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            publishedNoise = default;
            publishedCount = 0;
            world = new GameObject("PoltergeistTestWorld");
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
            soul = player.AddComponent<PlayerSoul>();
            soul.startingPowers = new[] { Power.Poltergeist };
            poltergeist = player.AddComponent<Poltergeist>();
            var lightObject = new GameObject("HandLight");
            lightObject.transform.SetParent(player.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            handLight = lightObject.AddComponent<Light>();
            handLight.type = LightType.Spot;
            handLight.range = 13f;
            handLight.spotAngle = 84f;
            poltergeist.handLight = handLight;

            witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(world.transform);
            witch.transform.position = new Vector3(0f, 14f, 0f);
            agent = witch.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            controller = witch.AddComponent<WitchController>();
            controller.player = player.transform;
            controller.skyRoute = new[] { new Vector3(30f, 14f, 0f), new Vector3(30f, 14f, 30f) };
            witch.SetActive(true);
            NoiseBus.Published += RecordNoise;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordNoise;
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
        public IEnumerator MovesLitObjectOnceAndSpendsEnergy()
        {
            MovableObject bottle = AddObject(ThrowableKind.Bottle, new Vector3(0f, 0.2f, 5f));
            Vector3 position = bottle.transform.position;
            Rigidbody body = bottle.GetComponent<Rigidbody>();
            Assert.IsTrue(body.isKinematic);

            Assert.IsTrue(poltergeist.TryMove());

            Assert.AreEqual(85f, soul.Energy.Current);
            Assert.IsTrue(bottle.Used);
            Assert.IsFalse(body.isKinematic);
            Assert.AreEqual(1, publishedCount);
            Assert.AreEqual(10f, publishedNoise.Radius);
            Assert.AreEqual(position, publishedNoise.Position);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(position, bottle.transform.position), 0.3f);

            Assert.IsFalse(poltergeist.TryMove());
            Assert.AreEqual(85f, soul.Energy.Current);
            Assert.AreEqual(1, publishedCount);
        }

        [UnityTest]
        public IEnumerator MovingBottlePlaysItsSound()
        {
            world.AddComponent<AudioListener>();
            MovableObject bottle = AddObject(ThrowableKind.Bottle, new Vector3(0f, 0.2f, 5f));
            var clip = AudioClip.Create("bottle", 22050, 1, 22050, false);
            bottle.sound = bottle.gameObject.AddComponent<AudioSource>();
            bottle.sound.playOnAwake = false;
            bottle.sound.spatialBlend = 0f;
            bottle.sound.clip = clip;
            try
            {
                yield return null;
                Assert.IsFalse(bottle.sound.isPlaying);

                Assert.IsTrue(poltergeist.TryMove());
                yield return null;
                Assert.IsTrue(bottle.sound.isPlaying);
            }
            finally
            {
                Object.Destroy(clip);
            }
        }

        [Test]
        public void IgnoresObjectsOutsideTheLight()
        {
            MovableObject bottle = AddObject(ThrowableKind.Bottle, new Vector3(0f, 0.2f, 5f));
            handLight.enabled = false;

            Assert.IsFalse(poltergeist.TryMove());
            Assert.AreEqual(100f, soul.Energy.Current);
            Assert.IsFalse(bottle.Used);
            Assert.AreEqual(0, publishedCount);

            SafeZones.Add(bottle.transform.position, 3f);

            Assert.IsTrue(poltergeist.TryMove());
            Assert.IsTrue(bottle.Used);
            Assert.AreEqual(85f, soul.Energy.Current);
        }

        [Test]
        public void IgnoresObjectsHiddenBehindAWall()
        {
            MovableObject bottle = AddObject(ThrowableKind.Bottle, new Vector3(0f, 0.2f, 6f));
            var wall = new GameObject("Wall");
            wall.transform.SetParent(world.transform);
            wall.transform.position = new Vector3(0f, 1.5f, 3f);
            wall.AddComponent<BoxCollider>().size = new Vector3(2f, 3f, 0.5f);
            Physics.SyncTransforms();

            Assert.IsFalse(poltergeist.TryMove());
            Assert.AreEqual(100f, soul.Energy.Current);
            Assert.IsFalse(bottle.Used);
            Assert.AreEqual(0, publishedCount);

            wall.SetActive(false);
            Physics.SyncTransforms();

            Assert.IsTrue(poltergeist.TryMove());
            Assert.AreEqual(85f, soul.Energy.Current);
            Assert.IsTrue(bottle.Used);
            Assert.AreEqual(1, publishedCount);
        }

        [Test]
        public void IgnoresObjectsBeyondTwelveMeters()
        {
            MovableObject bottle = AddObject(ThrowableKind.Bottle, new Vector3(0f, 0.2f, 12.5f));
            SafeZones.Add(bottle.transform.position, 3f);

            Assert.IsFalse(poltergeist.TryMove());

            Assert.AreEqual(100f, soul.Energy.Current);
            Assert.IsFalse(bottle.Used);
            Assert.AreEqual(0, publishedCount);
        }

        [Test]
        public void PicksTheObjectTiffanyFaces()
        {
            MovableObject pot = AddObject(ThrowableKind.Pot, new Vector3(4f, 0.2f, 0f));
            MovableObject can = AddObject(ThrowableKind.Can, new Vector3(0f, 0.2f, 4f));
            SafeZones.Add(Vector3.zero, 10f);

            Assert.IsTrue(poltergeist.TryMove());

            Assert.IsTrue(can.Used);
            Assert.IsFalse(pot.Used);
            Assert.AreEqual(can.transform.position, publishedNoise.Position);
            Assert.AreEqual(85f, soul.Energy.Current);
        }

        [UnityTest]
        public IEnumerator GroundedWitchWalksToTheNoise()
        {
            player.transform.position = new Vector3(20f, 0f, 0f);
            controller.playerLightOn = true;
            controller.playerLightPosition = player.transform.position;
            Physics.SyncTransforms();
            float deadline = Time.time + 6f;
            bool sawLanding = false;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stalking)
            {
                sawLanding |= controller.Decision.State == WitchState.Landing;
                yield return null;
            }
            Assert.IsTrue(sawLanding);
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            Assert.Less(Mathf.Abs(witch.transform.position.y), 0.3f);

            controller.playerLightOn = false;
            Vector3 position = witch.transform.position + Vector3.back * 18f;
            position.y = 0f;
            player.transform.position = position;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            MovableObject bell = AddObject(ThrowableKind.Bell, position + Vector3.forward * 8f + Vector3.up * 0.2f);
            SafeZones.Add(bell.transform.position, 1.5f);
            handLight.enabled = false;

            Assert.IsTrue(poltergeist.TryMove());
            Assert.AreEqual(1, publishedCount);
            Assert.AreEqual(16f, publishedNoise.Radius);
            Vector3 destination = SafeZones.PushOut(publishedNoise.Position, 1f);
            float initialDistance = HorizontalDistance(witch.transform.position, destination);
            deadline = Time.time + 1f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Suspicious) yield return null;
            Assert.AreEqual(WitchState.Suspicious, controller.Decision.State);
            Vector3 anchor = witch.transform.position;
            float suspicionStarted = Time.time;
            deadline = Time.time + controller.Profile.SuspicionDuration + 1f;
            while (Time.time < deadline && controller.Decision.State == WitchState.Suspicious)
            {
                Assert.AreEqual(anchor, witch.transform.position);
                Assert.AreEqual(0f, controller.Decision.Speed);
                yield return null;
            }
            Assert.GreaterOrEqual(Time.time - suspicionStarted, controller.Profile.SuspicionDuration - Time.deltaTime * 2f);
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsTrue(NavMesh.SamplePosition(destination, out var noisePoint, 6f, NavMesh.AllAreas));
            Assert.AreEqual(noisePoint.position, controller.Decision.MoveTarget);
            Assert.IsFalse(SafeZones.Contains(controller.Decision.MoveTarget));
            deadline = Time.time + 3f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            }

            float finalDistance = HorizontalDistance(witch.transform.position, destination);
            Debug.Log($"{GameInfo.LogPrefix} Poltergeist: distancia de la bruja al ruido de {initialDistance:0.##} m a {finalDistance:0.##} m");
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            Assert.Less(HorizontalDistance(agent.destination, destination), 1.5f);
            Assert.LessOrEqual(finalDistance, initialDistance - 1f);
        }

        private MovableObject AddObject(ThrowableKind kind, Vector3 position)
        {
            var target = new GameObject(kind.ToString());
            target.SetActive(false);
            target.transform.SetParent(world.transform);
            target.transform.position = position;
            target.AddComponent<BoxCollider>().size = Vector3.one * 0.4f;
            target.AddComponent<Rigidbody>();
            MovableObject movable = target.AddComponent<MovableObject>();
            movable.kind = kind;
            target.SetActive(true);
            Physics.SyncTransforms();
            return movable;
        }

        private void RecordNoise(StepNoise noise)
        {
            publishedNoise = noise;
            publishedCount++;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
