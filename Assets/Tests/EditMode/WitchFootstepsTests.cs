using System.Reflection;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchFootstepsTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        WitchAudio audio;
        PlayerFootsteps footsteps;
        WitchSoundRules rules;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WitchFootstepsTests");
            var player = new GameObject("Player");
            player.SetActive(false);
            player.transform.SetParent(root.transform, false);
            player.transform.position = Vector3.right * 10f;
            AudioBuilder.BuildFootsteps(player.AddComponent<PlayerController>());
            footsteps = player.GetComponent<PlayerFootsteps>();
            var witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(root.transform, false);
            var controller = witch.AddComponent<WitchController>();
            typeof(SceneBuilder).GetMethod("BuildWitchAudio", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { controller, player.transform, null });
            audio = witch.GetComponent<WitchAudio>();
            typeof(WitchAudio).GetMethod("Start", PrivateInstance).Invoke(audio, null);
            rules = (WitchSoundRules)typeof(WitchAudio).GetField("rules", PrivateInstance).GetValue(audio);
            SetDistance(0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(GroundType.Dirt, "step")]
        [TestCase(GroundType.Grass, "step_grass")]
        [TestCase(GroundType.Slab, "step_slab")]
        [TestCase(GroundType.Gravel, "step_gravel")]
        [TestCase(GroundType.Leaves, "step_leaves")]
        [TestCase(GroundType.Branches, "step_branches")]
        public void WitchUsesTiffanyClipsForItsOwnGroundWithoutConsecutiveRepeats(GroundType ground, string prefix)
        {
            Assert.AreSame(footsteps, audio.footsteps);
            Floor(Vector3.right * 10f, GroundType.Dirt);
            Floor(Vector3.zero, ground);
            var clips = (AudioClip[][])typeof(WitchAudio).GetField("stepClips", PrivateInstance).GetValue(audio);
            AudioClip previous = null;
            for (int i = 0; i < 20; i++)
            {
                var step = ChooseStep();
                CollectionAssert.Contains(clips[(int)ground], step.Clip);
                StringAssert.StartsWith($"Assets/Audio/Sfx/{prefix}_", AssetDatabase.GetAssetPath(step.Clip));
                Assert.AreNotSame(previous, step.Clip);
                Assert.AreEqual(0.3f, step.Volume, 1e-6f);
                previous = step.Clip;
            }
        }

        [TestCase(0f, 0.3f)]
        [TestCase(10f, 0.15f)]
        [TestCase(14f, 0.03f)]
        [TestCase(15f, 0f)]
        [TestCase(30f, 0f)]
        public void StepPlaybackUsesTiffanyVolumeWithDistanceAttenuation(float distance, float expected)
        {
            SetDistance(distance);
            Assert.AreEqual(expected, ChooseStep().Volume, 1e-6f);
        }

        [Test]
        public void StepVolumeFollowsTiffanyConfiguredWalkingVolume()
        {
            footsteps.walkVolume = 0.8f;
            typeof(WitchAudio).GetMethod("Start", PrivateInstance).Invoke(audio, null);
            SetDistance(10f);
            Assert.AreEqual(0.2f, ChooseStep().Volume, 1e-6f);
        }

        [Test]
        public void ExistingScenesResolveTiffanyFootstepsFromPlayer()
        {
            audio.footsteps = null;
            typeof(WitchAudio).GetMethod("Start", PrivateInstance).Invoke(audio, null);
            Assert.AreSame(footsteps, audio.footsteps);
            CollectionAssert.Contains(footsteps.dirt, ChooseStep().Clip);
        }

        [Test]
        public void MissingOrUntaggedGroundUsesDirt()
        {
            CollectionAssert.Contains(footsteps.dirt, ChooseStep().Clip);
            var floor = Floor(Vector3.zero, GroundType.Grass);
            Object.DestroyImmediate(floor.GetComponent<GroundSurface>());
            CollectionAssert.Contains(footsteps.dirt, ChooseStep().Clip);
        }

        [Test]
        public void GroundRayIgnoresTriggersAndUsesNearestSurfaceWithinWitchMask()
        {
            Floor(Vector3.down * 0.1f, GroundType.Dirt);
            Floor(Vector3.zero, GroundType.Leaves);
            Floor(Vector3.up * 0.1f, GroundType.Gravel).GetComponent<Collider>().isTrigger = true;
            var excluded = Floor(Vector3.up * 0.2f, GroundType.Slab);
            excluded.layer = LayerMask.NameToLayer("Ignore Raycast");
            Assert.That(ChooseStep().Clip.name, Does.StartWith("step_leaves_"));
        }

        [Test]
        public void MissingGroundClipsFallBackToDirtAndEmptyDirtIsSilent()
        {
            Floor(Vector3.zero, GroundType.Grass);
            footsteps.grass = null;
            typeof(WitchAudio).GetMethod("Start", PrivateInstance).Invoke(audio, null);
            CollectionAssert.Contains(footsteps.dirt, ChooseStep().Clip);
            footsteps.dirt = new AudioClip[0];
            typeof(WitchAudio).GetMethod("Start", PrivateInstance).Invoke(audio, null);
            Assert.IsNull(ChooseStep().Clip);
        }

        GameObject Floor(Vector3 position, GroundType type)
        {
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = position - Vector3.up * 0.05f;
            floor.AddComponent<BoxCollider>().size = new Vector3(4f, 0.1f, 4f);
            floor.AddComponent<GroundSurface>().type = type;
            return floor;
        }

        void SetDistance(float distance) => rules.Tick(1f,
            new WitchDecision { Form = WitchForm.Grounded, State = WitchState.Stalking, Speed = 1.2f }, distance);

        (AudioClip Clip, float Volume) ChooseStep()
        {
            Physics.SyncTransforms();
            return ((AudioClip Clip, float Volume))typeof(WitchAudio).GetMethod("ChooseStep", PrivateInstance)
                .Invoke(audio, null);
        }
    }
}
