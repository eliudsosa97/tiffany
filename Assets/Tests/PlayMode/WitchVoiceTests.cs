using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class WitchVoiceTests
    {
        private GameObject world;
        private NavMeshSurface surface;
        private WitchVoice voice;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            SafeZones.Clear();
            world = new GameObject("WitchVoiceTestWorld");
            var ground = world.AddComponent<BoxCollider>();
            ground.size = new Vector3(80f, 1f, 80f);
            ground.center = Vector3.down * 0.5f;
            surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            var witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(world.transform);
            witch.transform.position = Vector3.up * 14f;
            witch.AddComponent<NavMeshAgent>().enabled = false;
            var controller = witch.AddComponent<WitchController>();
            controller.player = new GameObject("Player").transform;
            controller.player.SetParent(world.transform);
            controller.player.position = new Vector3(-30f, 0f, -30f);
            voice = witch.AddComponent<WitchVoice>();
            voice.witch = controller;
            voice.source = witch.AddComponent<AudioSource>();
            voice.echo = witch.AddComponent<AudioSource>();
            voice.source.playOnAwake = voice.echo.playOnAwake = false;
            voice.barks = new[] { AudioClip.Create("Bark", 4410, 1, 44100, false) };
            witch.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SafeZones.Clear();
            surface.RemoveData();
            Object.Destroy(surface.navMeshData);
            Object.Destroy(voice.barks[0]);
            Object.Destroy(world);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundedLureMimicsWithinThreeSecondsAndRespectsPause()
        {
            voice.witch.raining = true;
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(WitchState.Stalking, voice.witch.Decision.State);
            Assert.IsTrue(voice.witch.Decision.VoiceLure);
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreEqual(0, voice.Count);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(1, voice.Count);
            Assert.AreSame(voice.barks[0], voice.echo.clip);
            Assert.AreEqual(voice.source.pitch, voice.echo.pitch);
        }

        [UnityTest]
        public IEnumerator FlyingDoesNotMimic()
        {
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(WitchForm.Flying, voice.witch.Decision.Form);
            Assert.AreEqual(0, voice.Count);
        }
    }
}
