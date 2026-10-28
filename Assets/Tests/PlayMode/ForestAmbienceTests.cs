using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace TiffanyGame.Tests
{
    public sealed class ForestAmbienceTests
    {
        private GameObject root;
        private AudioClip clip;
        [UnityTest]
        public IEnumerator LakeWaterPlaysAndFadesOutAtTheStable()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            root = new GameObject("Ambience Test");
            var audio = root.AddComponent<ForestAmbience>();
            audio.player = new GameObject("Player", typeof(AudioListener)).transform;
            audio.player.SetParent(root.transform);
            audio.clearings = new Transform[8];
            foreach (int index in new[] { 3, 5 })
            {
                var clearing = new GameObject("Clearing").transform;
                clearing.SetParent(root.transform);
                clearing.position = index == 5 ? Vector3.up * 100f : Vector3.right * 60f;
                audio.clearings[index] = clearing;
            }
            clip = AudioClip.Create("Ambience Loop", 44100, 1, 44100, false);
            audio.loops = new AudioSource[5];
            audio.loopClips = new[] { clip, clip, clip, clip, clip };
            for (int i = 0; i < 5; i++)
            {
                audio.loops[i] = root.AddComponent<AudioSource>();
                (audio.loops[i].loop, audio.loops[i].playOnAwake, audio.loops[i].spatialBlend) = (true, false, 0f);
            }
            var water = audio.loops[(int)AmbienceLayer.Lake];
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual((true, 0.6f), (water.isPlaying, water.volume));
            audio.player.position = audio.clearings[3].position;
            yield return null;
            Assert.Greater(water.volume, 0f);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual((false, 0f), (water.isPlaying, water.volume));
            Assert.IsTrue(audio.loops[(int)AmbienceLayer.Leaves].isPlaying);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            (Time.timeScale, AudioListener.pause) = (1f, false);
            Object.Destroy(root);
            Object.Destroy(clip);
            yield return null;
        }
    }
}
