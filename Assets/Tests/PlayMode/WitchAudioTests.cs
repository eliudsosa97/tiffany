using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace TiffanyGame.Tests
{
    public sealed class WitchAudioTests
    {
        private readonly WitchControllerTests fixture = new WitchControllerTests();
        private WitchAudio audio;
        private AudioClip clip;
        [UnityTest]
        public IEnumerator FlightAndStalkingUseDifferentLoopsAndPauseResumesBoth()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            fixture.SetUp();
            var controller = fixture.Controller;
            controller.player.gameObject.AddComponent<AudioListener>();
            controller.player.position = Vector3.right * 4f;
            controller.playerVanished = true;
            var witch = controller.gameObject;
            audio = witch.AddComponent<WitchAudio>();
            audio.witch = controller;
            audio.player = controller.player;
            audio.fireLoop = witch.AddComponent<AudioSource>();
            audio.breathLoop = witch.AddComponent<AudioSource>();
            audio.oneShot = witch.AddComponent<AudioSource>();
            audio.fireLoop.playOnAwake = audio.breathLoop.playOnAwake = audio.oneShot.playOnAwake = false;
            clip = AudioClip.Create("Loop", 44100, 1, 44100, false);
            audio.fireLoop.clip = audio.breathLoop.clip = clip;
            audio.fireLoop.loop = audio.breathLoop.loop = true;
            yield return null;
            yield return null;
            Assert.AreEqual(WitchForm.Flying, audio.witch.Decision.Form);
            Assert.IsTrue(audio.fireLoop.isPlaying);
            Assert.IsFalse(audio.breathLoop.isPlaying);
            yield return CheckPause(audio.fireLoop);
            audio.witch.raining = true;
            float deadline = Time.time + 6f;
            while (audio.witch.Decision.State != WitchState.Stalking && Time.time < deadline) yield return null;
            yield return null;
            Assert.AreEqual(WitchState.Stalking, audio.witch.Decision.State);
            Assert.IsTrue(audio.breathLoop.isPlaying);
            Assert.IsFalse(audio.fireLoop.isPlaying);
            yield return CheckPause(audio.breathLoop);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            Object.Destroy(clip);
            yield return fixture.TearDown();
        }
        private IEnumerator CheckPause(AudioSource source)
        {
            Time.timeScale = 0f;
            yield return null;
            Assert.IsFalse(audio.fireLoop.isPlaying || audio.breathLoop.isPlaying);
            int sample = source.timeSamples;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(sample, source.timeSamples);
            Time.timeScale = 1f;
            yield return null;
            Assert.IsTrue(source.isPlaying);
        }
    }
}
