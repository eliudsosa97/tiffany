using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace TiffanyGame.Tests
{
    public sealed class TensionMusicTests
    {
        private readonly WitchControllerTests fixture = new WitchControllerTests();
        private TensionMusic music;
        private AudioClip clip;
        [SetUp]
        public void SetUp()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            fixture.SetUp();
            var controller = fixture.Controller;
            controller.chapter = Chapter.Return;
            controller.ResetBrain();
            controller.enabled = false;
            controller.player.gameObject.AddComponent<AudioListener>();
            music = controller.gameObject.AddComponent<TensionMusic>();
            (music.witch, music.player) = (controller, controller.player);
            music.drone = controller.gameObject.AddComponent<AudioSource>();
            music.pulse = controller.gameObject.AddComponent<AudioSource>();
            clip = AudioClip.Create("Tension Loop", 44100 * 4, 1, 44100, false);
            foreach (var source in new[] { music.drone, music.pulse })
                (source.clip, source.loop, source.playOnAwake, source.spatialBlend) = (clip, true, false, 0f);
        }
        [UnityTest]
        public IEnumerator StalkingAndHidingKeepMusicStoppedWhileAmbienceTensionChanges()
        {
            music.player.position += Vector3.up * 50f;
            yield return new WaitForSeconds(TensionMusicRules.FadeIn + 0.1f);
            Assert.AreEqual((WitchForm.Grounded, WitchState.Stalking), (music.witch.Decision.Form, music.witch.Decision.State));
            Assert.AreEqual((false, false), (music.drone.isPlaying, music.pulse.isPlaying));
            Assert.AreEqual((0f, 0f, 1f), (music.drone.volume, music.pulse.volume, music.Level));
            music.player.gameObject.AddComponent<PlayerController>().enabled = false;
            music.hiding = music.player.gameObject.AddComponent<PlayerHiding>();
            music.hiding.enabled = false;
            var bush = new GameObject("Hide", typeof(BoxCollider), typeof(HideVolume));
            bush.transform.SetParent(music.player, false);
            Physics.SyncTransforms();
            music.hiding.Hide(bush.GetComponent<HideVolume>());
            yield return new WaitForSeconds(TensionMusicRules.FadeOut + 0.1f);
            Assert.AreEqual((false, false), (music.drone.isPlaying, music.pulse.isPlaying));
            Assert.AreEqual((0f, 0f, 0f), (music.drone.volume, music.pulse.volume, music.Level));
        }
        [UnityTest]
        public IEnumerator PauseFreezesAmbienceTensionAndNeverResumesMusic()
        {
            music.witch.Profile.CanSprint = false;
            music.witch.enabled = true;
            yield return new WaitForSeconds(TensionMusicRules.FadeIn + 0.1f);
            Assert.AreEqual((false, false), (music.drone.isPlaying, music.pulse.isPlaying));
            Time.timeScale = 0f;
            yield return null;
            Assert.AreEqual((false, false), (music.drone.isPlaying, music.pulse.isPlaying));
            float level = music.Level;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(level, music.Level);
            Time.timeScale = 1f;
            yield return null;
            Assert.AreEqual((false, false), (music.drone.isPlaying, music.pulse.isPlaying));
            Assert.AreEqual((0f, 0f), (music.drone.volume, music.pulse.volume));
        }
        [UnityTest]
        public IEnumerator AmbienceLoopsDuckWhenMusicIsAssigned()
        {
            var ambience = music.player.gameObject.AddComponent<ForestAmbience>();
            ambience.player = music.player;
            var loop = music.player.gameObject.AddComponent<AudioSource>();
            (loop.loop, loop.playOnAwake, loop.spatialBlend) = (true, false, 0f);
            ambience.loops = new[] { loop };
            ambience.loopClips = new[] { clip };
            yield return new WaitForSeconds(TensionMusicRules.FadeIn + 0.1f);
            Assert.AreEqual(AmbienceMix.CemeteryCrickets, loop.volume, 1e-5f);
            ambience.music = music;
            yield return null;
            Assert.AreEqual(AmbienceMix.CemeteryCrickets * TensionMusicRules.AmbienceDuck(music.Level), loop.volume, 1e-5f);
        }
        [UnityTest]
        public IEnumerator WindUpSilencesAmbienceEvenWithoutMusicSources()
        {
            var ambience = music.player.gameObject.AddComponent<ForestAmbience>();
            ambience.player = music.player;
            ambience.music = music;
            var loop = music.player.gameObject.AddComponent<AudioSource>();
            (loop.loop, loop.playOnAwake, loop.spatialBlend) = (true, false, 0f);
            ambience.loops = new[] { loop };
            ambience.loopClips = new[] { clip };
            music.drone = music.pulse = null;
            yield return new WaitForSeconds(TensionMusicRules.FadeIn + 0.1f);
            Assert.Greater(loop.volume, 0f);
            Assert.IsTrue(loop.isPlaying);
            music.player.position = music.witch.transform.position + Vector3.right * 3f;
            music.witch.playerLightOn = true;
            music.witch.playerLightPosition = music.player.position;
            music.witch.Profile.WindUpDuration = 2f;
            Physics.SyncTransforms();
            music.witch.enabled = true;
            float deadline = Time.time + 8f;
            while (Time.time < deadline && music.witch.Decision.State != WitchState.WindUp) yield return null;
            Assert.AreEqual(WitchState.WindUp, music.witch.Decision.State);
            music.witch.enabled = false;
            yield return new WaitForSeconds(TensionMusicRules.SilenceFade + 0.1f);
            Assert.AreEqual(1f, music.Silence);
            Assert.AreEqual(0f, loop.volume);
            Assert.IsFalse(loop.isPlaying);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            Object.Destroy(clip);
            yield return fixture.TearDown();
        }
    }
}
