using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public sealed class EchoPlayerTests
    {
        GameObject root;
        EchoPlayer player;
        TextAsset asset;
        AudioClip clip;
        AudioSource source;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            root = new GameObject("Echo Audio Test");
            root.SetActive(false);
            root.AddComponent<AudioListener>();
            player = root.AddComponent<EchoPlayer>();
            clip = AudioClip.Create("test-audio", 44100, 1, 44100, false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var data = new EchoData
            {
                id = 1, duration = 10f,
                girl = new EchoTrack { disappear = 10f }, tyler = new EchoTrack { disappear = 10f },
                subtitles = new[] { new EchoSubtitle { time = 0f, duration = 1f, text = "Ignorado" } },
                sounds = new[] { new EchoSound { clip = clip.name, time = 0f, duration = 1f, pitch = 1.5f, volume = 0.6f } }
            };
            asset = new TextAsset(JsonUtility.ToJson(data));
            player.echoAsset = asset;
            player.clips = new[] { clip };
            player.sources = new[] { source };
            root.SetActive(true);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(asset);
            Object.Destroy(clip);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            yield return null;
        }

        [UnityTest] public IEnumerator AudioPlaysWhileTimeAndListenerArePausedAndStopsAtSlideshowEnd()
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            var show = Presentation();
            player.Play(show);
            Assert.IsTrue(player.IsPlaying);
            Assert.IsTrue(source.ignoreListenerPause);
            Assert.AreSame(clip, source.clip);
            Assert.AreEqual(1.5f, source.pitch);
            Assert.AreEqual(0.6f, source.volume);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.IsTrue(source.isPlaying);
            Assert.Greater(source.time, 0f);
            show.Tick(5f);
            player.ApplyAudio();
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(source.isPlaying);
        }

        [UnityTest] public IEnumerator ReplayAndDisableStopTheAudioImmediately()
        {
            player.Play(Presentation());
            yield return null;
            player.Play(Presentation());
            Assert.AreEqual(0f, player.Elapsed);
            player.enabled = false;
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(source.isPlaying);
        }

        MemorySlideshow Presentation() => new MemorySlideshow(
            new MemoryData { id = 1, drawings = new[] { new MemoryDrawing() } }, player.PrepareAudio());
    }
}
