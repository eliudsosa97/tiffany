using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class StormTests
    {
        [UnityTest]
        public IEnumerator RainAndThunderReachWitchAndStopWithRain()
        {
            var witchObject = new GameObject("Witch");
            witchObject.SetActive(false);
            var agent = witchObject.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            var witch = witchObject.AddComponent<WitchController>();
            witchObject.SetActive(true);
            var storm = new GameObject("Storm").AddComponent<Storm>();
            try
            {
                storm.raining = true;
                storm.Strike();
                yield return new WaitForSeconds(2.2f);
                Assert.IsTrue(witch.raining);
                Assert.IsTrue(witch.hearingMuted);

                storm.raining = false;
                yield return null;
                Assert.IsFalse(witch.hearingMuted);
                Assert.IsFalse(witch.raining);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.Destroy(storm.gameObject);
                Object.Destroy(witchObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RainReachesPlayerAndScriptedLightningExpires()
        {
            var ground = new GameObject("Ground", typeof(BoxCollider));
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var playerObject = new GameObject("Player", typeof(CharacterController));
            var controller = playerObject.GetComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = Vector3.up * 0.6f;
            var player = playerObject.AddComponent<PlayerController>();
            player.viewCamera = cameraObject.GetComponent<Camera>();
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.lightning = new GameObject("Lightning").AddComponent<Light>();
            storm.lightning.transform.SetParent(storm.transform);
            storm.lightning.enabled = false;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = GameInput.SlowOverride = false;
            try
            {
                Physics.SyncTransforms();
                storm.raining = true;
                yield return null;
                Assert.IsTrue(player.raining);
                storm.Strike();
                yield return null;
                Assert.IsTrue(storm.Flashing && storm.lightning.enabled);
                Assert.Greater(storm.lightning.intensity, 0f);
                yield return new WaitForSeconds(1.2f);
                Assert.IsFalse(storm.Flashing || storm.lightning.enabled);
                Assert.AreEqual(0f, storm.lightning.intensity);
            }
            finally
            {
                foreach (var instance in new[] { storm.gameObject, playerObject, cameraObject, ground }) Object.Destroy(instance);
                GameInput.ClearOverrides();
            }
            yield return null;
        }
    }
}
