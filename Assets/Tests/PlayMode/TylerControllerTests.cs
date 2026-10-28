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
        public IEnumerator FollowsPlayerOnNavMeshWithoutColliders()
        {
            Assert.IsNotNull(tyler.nextMemory);
            Assert.IsNotNull(tyler.voice);
            Assert.AreEqual(0f, tyler.voice.spatialBlend);
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
            float deadline = Time.time + 6f;
            while (Time.time < deadline && HorizontalDistance() > 3f)
            {
                yield return null;
                moved |= tyler.Speed > 0f;
            }
            Assert.IsTrue(moved);
            Assert.LessOrEqual(HorizontalDistance(), 3f);
            Assert.IsTrue(tyler.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.IsEmpty(tyler.GetComponentsInChildren<Collider>(true));
            Assert.IsTrue(tyler.NearPlayer);
        }

        [UnityTest]
        public IEnumerator CaptureStagesRestoreControllerAndNavigation()
        {
            var stages = Object.FindObjectsByType<TylerCaptureStage>(FindObjectsSortMode.None);
            Assert.AreEqual(4, stages.Length);
            foreach (var stage in stages)
            {
                tyler.gameObject.SetActive(false);
                stage.Apply(tyler.player);
                Assert.IsTrue(tyler.gameObject.activeSelf);
                Assert.IsFalse(tyler.enabled);
                Assert.IsFalse(tyler.GetComponent<NavMeshAgent>().enabled);
                Assert.AreEqual(stage.reaction, tyler.Reaction);
                Assert.AreEqual(stage.reaction == TylerReaction.Growl, tyler.crouchBody.activeSelf);
                Assert.AreEqual(stage.reaction != TylerReaction.Growl, tyler.standBody.activeSelf);
                stage.Clear();
                yield return null;
                Assert.IsTrue(tyler.enabled && tyler.GetComponent<NavMeshAgent>().isOnNavMesh);
            }
        }

        private float HorizontalDistance() =>
            Vector3.ProjectOnPlane(tyler.player.position - tyler.transform.position, Vector3.up).magnitude;
    }
}
