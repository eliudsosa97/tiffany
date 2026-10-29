using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class StormTests : TiffanyLogTests
    {
        [UnityTest]
        public IEnumerator ChapterScheduleKeepsOutboundDryAndStartsRainOnReturn()
        {
            var witchObject = new GameObject("Witch");
            witchObject.SetActive(false);
            witchObject.AddComponent<NavMeshAgent>().enabled = false;
            var witch = witchObject.AddComponent<WitchController>();
            witch.enabled = false;
            witch.chapter = Chapter.Cemetery;
            witchObject.SetActive(true);
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.witch = witch;
            try
            {
                storm.raining = true;
                storm.Strike();
                yield return null;
                Assert.IsFalse(storm.raining);
                Assert.IsFalse(storm.Flashing);
                Assert.IsFalse(witch.raining);

                witch.chapter = Chapter.Return;
                yield return null;
                Assert.IsTrue(storm.raining);
                Assert.IsTrue(witch.raining);
                storm.Strike();
                yield return null;
                Assert.IsTrue(storm.Flashing);
                storm.raining = false;
                yield return null;
                Assert.IsTrue(storm.raining);
            }
            finally
            {
                Object.Destroy(storm.gameObject);
                Object.Destroy(witchObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureOverridesChapterWeatherAndRestoresItAfterCleanup()
        {
            var witchObject = new GameObject("Witch");
            witchObject.SetActive(false);
            witchObject.AddComponent<NavMeshAgent>().enabled = false;
            var witch = witchObject.AddComponent<WitchController>();
            witch.enabled = false;
            witch.chapter = Chapter.Cemetery;
            witchObject.SetActive(true);
            var storm = new GameObject("Storm").AddComponent<Storm>();
            storm.witch = witch;
            var stage = storm.gameObject.AddComponent<StormCaptureStage>();
            try
            {
                yield return null;
                stage.raining = stage.lightning = true;
                stage.Apply();
                yield return null;
                Assert.IsFalse(storm.followChapter);
                Assert.IsTrue(storm.raining);
                Assert.IsTrue(storm.Flashing);
                stage.Clear();
                yield return null;
                Assert.IsTrue(storm.followChapter);
                Assert.IsFalse(storm.raining);
                Assert.IsFalse(storm.Flashing);

                witch.chapter = Chapter.Return;
                yield return null;
                Assert.IsTrue(storm.raining);
                stage.raining = stage.lightning = false;
                stage.Apply();
                yield return null;
                Assert.IsFalse(storm.raining);
                stage.Clear();
                yield return null;
                Assert.IsTrue(storm.followChapter);
                Assert.IsTrue(storm.raining);

                storm.followChapter = false;
                stage.Apply();
                yield return null;
                Assert.IsFalse(storm.raining);
                stage.Clear();
                yield return null;
                Assert.IsFalse(storm.followChapter);
                Assert.IsTrue(storm.raining);
            }
            finally
            {
                stage.Clear();
                Object.Destroy(storm.gameObject);
                Object.Destroy(witchObject);
            }
            yield return null;
        }

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
            storm.moon = new GameObject("Moon").AddComponent<Light>();
            storm.moon.transform.SetParent(storm.transform);
            storm.moon.type = LightType.Directional;
            storm.moon.intensity = 0.3f;
            var originalAmbient = RenderSettings.ambientLight;
            var baseAmbient = new Color(0.025f, 0.035f, 0.065f);
            RenderSettings.ambientLight = baseAmbient;
            GameInput.MoveOverride = Vector2.zero;
            GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = false;
            GameInput.SlowOverride = false;
            try
            {
                Physics.SyncTransforms();
                storm.raining = true;
                yield return null;
                Assert.IsTrue(player.raining);
                Assert.AreEqual(0.3f, storm.moon.intensity);
                Assert.AreEqual(baseAmbient, RenderSettings.ambientLight);
                storm.Strike();
                yield return null;
                Assert.IsTrue(storm.Flashing);
                Assert.Greater(storm.moon.intensity, 0.3f);
                Assert.AreNotEqual(baseAmbient, RenderSettings.ambientLight);
                yield return new WaitForSeconds(1.2f);
                Assert.IsFalse(storm.Flashing);
                Assert.AreEqual(0.3f, storm.moon.intensity);
                Assert.AreEqual(baseAmbient, RenderSettings.ambientLight);

                storm.Strike();
                yield return null;
                Assert.Greater(storm.moon.intensity, 0.3f);
                storm.enabled = false;
                Assert.AreEqual(0.3f, storm.moon.intensity);
                Assert.AreEqual(baseAmbient, RenderSettings.ambientLight);
            }
            finally
            {
                storm.enabled = false;
                RenderSettings.ambientLight = originalAmbient;
                foreach (var instance in new[] { storm.gameObject, playerObject, cameraObject, ground }) Object.Destroy(instance);
                GameInput.ClearOverrides();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RainAudioLoopsOnlyWhileRaining()
        {
            var storm = new GameObject("Storm", typeof(AudioListener)).AddComponent<Storm>();
            var clip = AudioClip.Create("Rain", 4410, 1, 44100, false);
            storm.rainSource = storm.gameObject.AddComponent<AudioSource>();
            storm.rainSource.playOnAwake = false;
            storm.rainSource.loop = true;
            storm.rainSource.clip = clip;
            try
            {
                yield return null;
                Assert.IsFalse(storm.rainSource.isPlaying);

                storm.raining = true;
                yield return null;
                Assert.IsTrue(storm.rainSource.isPlaying);
                yield return new WaitForSeconds(0.2f);
                Assert.IsTrue(storm.rainSource.isPlaying);

                storm.raining = false;
                yield return null;
                Assert.IsFalse(storm.rainSource.isPlaying);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.Destroy(storm.gameObject);
                Object.Destroy(clip);
            }
            yield return null;
        }
    }
}
