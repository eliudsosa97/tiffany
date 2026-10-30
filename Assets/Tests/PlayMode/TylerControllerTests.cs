using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class TylerControllerTests
    {
        private TylerController tyler;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            tyler = Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            Assert.IsNotNull(tyler);
            Assert.IsNotNull(tyler.witch);
            tyler.witch.enabled = false;
            tyler.witch.GetComponent<NavMeshAgent>().enabled = false;
            tyler.witch.transform.position = Vector3.one * 1000f;
            tyler.Appear(tyler.player.position);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(TylerControllerTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator SceneTylerUsesOneAnimatorWithItsControllerAndSpiritMaterials()
        {
            Assert.IsNotNull(tyler.figure);
            Assert.AreSame(tyler.transform, tyler.figure.transform.parent);
            Assert.AreEqual(Vector3.zero, tyler.figure.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, tyler.figure.transform.localRotation);
            Assert.AreEqual(Vector3.one, tyler.figure.transform.localScale);
            var animators = tyler.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(1, animators.Length);
            Assert.AreSame(animators[0], tyler.animator);
            Assert.AreSame(tyler.figure, tyler.animator.gameObject);
            Assert.IsNotNull(tyler.animator.runtimeAnimatorController);
            Assert.AreEqual("Tyler", tyler.animator.runtimeAnimatorController.name);
#if UNITY_EDITOR
            Assert.AreSame(UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Models/Tyler.controller"),
                tyler.animator.runtimeAnimatorController);
#endif
            Assert.IsFalse(tyler.animator.applyRootMotion);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, tyler.animator.cullingMode);
            Assert.IsNotNull(tyler.head);
            Assert.AreEqual("Head", tyler.head.name);
            var renderers = tyler.figure.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
            {
                Assert.IsInstanceOf<SkinnedMeshRenderer>(renderer);
                CollectionAssert.Contains(((SkinnedMeshRenderer)renderer).bones, tyler.head);
                Assert.IsTrue(renderer.enabled);
                var materials = renderer.sharedMaterials;
                Assert.AreEqual(5, materials.Length);
                Assert.AreEqual("Universal Render Pipeline/Tiffany Silhouette", materials[4].shader.name);
                for (int i = 0; i < 4; i++)
                {
                    var material = materials[i];
                    Assert.IsNotNull(material);
                    Assert.AreEqual("TiffanyGame/Spirit", material.shader.name);
                }
            }
            Assert.IsEmpty(tyler.GetComponentsInChildren<Collider>(true));
            yield break;
        }

        [UnityTest]
        public IEnumerator FollowsPlayerOnNavMeshWithoutColliders()
        {
            Assert.IsNotNull(tyler.nextMemory);
            Assert.IsNotNull(tyler.voice);
            Assert.AreEqual(1f, tyler.voice.spatialBlend);
            Assert.AreEqual(AudioRolloffMode.Linear, tyler.voice.rolloffMode);
            Assert.AreEqual(2f, tyler.voice.minDistance);
            Assert.AreEqual(30f, tyler.voice.maxDistance);
            Assert.AreEqual(0f, tyler.voice.dopplerLevel);
            Assert.IsFalse(tyler.voice.playOnAwake);
            tyler.nextMemory = null;
            tyler.player.GetComponent<PlayerController>().enabled = false;
            var character = tyler.player.GetComponent<CharacterController>();
            character.enabled = false;
            tyler.player.position = tyler.transform.position + Vector3.right * 8f;
            character.enabled = true;
            Physics.SyncTransforms();
            Assert.IsFalse(tyler.NearPlayer);
            bool moved = false;
            bool animatedWalk = false;
            float deadline = Time.time + 6f;
            while (Time.time < deadline && HorizontalDistance() > 3f)
            {
                yield return null;
                moved |= tyler.Speed > 0f;
                animatedWalk |= tyler.animator.GetFloat(TylerAnimation.SpeedParameter) > TylerMotion.StillSpeed
                    && (tyler.animator.GetCurrentAnimatorStateInfo(0).IsName(TylerAnimation.Walk)
                        || tyler.animator.GetNextAnimatorStateInfo(0).IsName(TylerAnimation.Walk));
            }
            Assert.IsTrue(moved);
            Assert.IsTrue(animatedWalk);
            Assert.LessOrEqual(HorizontalDistance(), 3f);
            Assert.IsTrue(tyler.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.IsEmpty(tyler.GetComponentsInChildren<Collider>(true));
            Assert.IsTrue(tyler.NearPlayer);
        }

        [UnityTest]
        public IEnumerator MimicIsIgnoredWhileInactiveAndHeardAfterAppearing()
        {
            tyler.gameObject.SetActive(false);
            tyler.HearMimic();
            tyler.Appear(tyler.player.position);
            tyler.witch.transform.position = tyler.transform.position + Vector3.forward * 25f;
            yield return null;
            Assert.AreEqual(TylerReaction.Whine, tyler.Reaction);

            tyler.HearMimic();
            yield return null;
            Assert.AreEqual(TylerReaction.Growl, tyler.Reaction);
        }

        [UnityTest]
        public IEnumerator ReappearingRestoresTheSameReactionAnimation()
        {
            Vector3 target = tyler.transform.position + Vector3.forward * 10f;
            tyler.ShowReaction(TylerReaction.Growl, target);
            AssertAnimation(TylerAnimation.Growl);
            tyler.gameObject.SetActive(false);
            tyler.Appear(tyler.player.position);
            tyler.ShowReaction(TylerReaction.Growl, target);
            AssertAnimation(TylerAnimation.Growl);
            yield break;
        }

        [UnityTest]
        public IEnumerator CaptureStagesRestoreControllerAndNavigation()
        {
            var stages = Object.FindObjectsByType<TylerCaptureStage>(FindObjectsSortMode.None);
            Assert.AreEqual(5, stages.Length);
            foreach (var stage in stages)
            {
                tyler.gameObject.SetActive(false);
                stage.Apply(tyler.player);
                var lightStage = stage.GetComponent<LightCaptureStage>();
                if (lightStage != null) lightStage.Apply(tyler.player);
                Assert.IsTrue(tyler.gameObject.activeSelf);
                Assert.AreEqual(stage.shareLight, tyler.enabled);
                Assert.IsFalse(tyler.GetComponent<NavMeshAgent>().enabled);
                Assert.AreEqual(stage.reaction, tyler.Reaction);
                Assert.IsTrue(tyler.figure.activeInHierarchy);
                string animation = stage.reaction == TylerReaction.Growl ? TylerAnimation.Growl
                    : stage.reaction == TylerReaction.Whine ? TylerAnimation.Whine : TylerAnimation.Idle;
                AssertAnimation(animation);
                if (stage.shareLight)
                {
                    Assert.IsFalse(stage.lightTyler);
                    Assert.IsNull(stage.fillLight);
                    yield return null;
                    Assert.IsTrue(tyler.SharingLight && tyler.shareLight.enabled);
                    Assert.AreEqual(0f, tyler.soul.Energy.Current);
                }
                stage.Clear();
                if (stage.shareLight) Assert.IsFalse(tyler.SharingLight || tyler.shareLight.enabled);
                if (lightStage != null) lightStage.Clear();
                yield return null;
                Assert.IsTrue(tyler.enabled && tyler.GetComponent<NavMeshAgent>().isOnNavMesh);
            }
        }

        private void AssertAnimation(string state)
        {
            for (int i = 0; i < 10; i++) tyler.animator.Update(0.1f);
            Assert.IsFalse(tyler.animator.IsInTransition(0));
            Assert.IsTrue(tyler.animator.GetCurrentAnimatorStateInfo(0).IsName(state), state);
        }

        private float HorizontalDistance() =>
            Vector3.ProjectOnPlane(tyler.player.position - tyler.transform.position, Vector3.up).magnitude;
    }
}
