using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class HeartbeatTests
    {
        static readonly float[] Distances = { 4f, 40f };
        GameObject player;
        WitchController witch;
        PlayerLight light;
        PanicController panic;
        Heartbeat heartbeat;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            Time.timeScale = 1f;
            GameInput.LightToggleOverride = false;
            GameInput.FlashOverride = false;
            var witchObject = new GameObject("Witch");
            witchObject.SetActive(false);
            witchObject.AddComponent<NavMeshAgent>().enabled = false;
            witch = witchObject.AddComponent<WitchController>();
            witch.enabled = false;
            witch.transform.position = new Vector3(4f, 100f, 0f);
            witchObject.SetActive(true);
            player = new GameObject("Tiffany", typeof(AudioListener));
            player.SetActive(false);
            player.AddComponent<PlayerSoul>().startingPowers = new[] { Power.Light };
            light = player.AddComponent<PlayerLight>();
            light.spot = new GameObject("Light").AddComponent<Light>();
            light.spot.transform.SetParent(player.transform, false);
            light.startOn = true;
            panic = player.AddComponent<PanicController>();
            panic.playerLight = light;
            panic.witch = witch;
            heartbeat = player.AddComponent<Heartbeat>();
            heartbeat.panic = panic;
            heartbeat.witch = witch;
            heartbeat.source = player.AddComponent<AudioSource>();
            heartbeat.source.playOnAwake = false;
            heartbeat.source.spatialBlend = 0f;
            heartbeat.clip = AudioClip.Create("Heartbeat", 27342, 1, 44100, false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(heartbeat.clip);
            Object.Destroy(player);
            Object.Destroy(witch.gameObject);
            SafeZones.Clear();
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnlyNearbyWitchProducesBeatsWithLightOn([ValueSource(nameof(Distances))] float distance)
        {
            witch.BeginWatch(player.transform, new Vector3(distance, 100f, 0f), false);
            witch.enabled = true;
            Time.timeScale = 0f;
            player.SetActive(true);
            yield return null;
            Assert.IsTrue(witch.isActiveAndEnabled);
            Assert.AreEqual(WitchForm.Grounded, witch.Decision.Form);
            Assert.AreEqual(0, heartbeat.Beats);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(light.State.Visible);
            if (distance < PanicModel.WitchRadius) Assert.GreaterOrEqual(heartbeat.Beats, 3);
            else Assert.AreEqual(0, heartbeat.Beats);
        }

        [UnityTest]
        public IEnumerator FlyingWitchDirectlyAboveCalmPlayerProducesNoBeats()
        {
            witch.transform.position = player.transform.position + Vector3.up * 7.9f;
            witch.enabled = true;
            panic.enabled = false;
            panic.Model.ResetTo(0f);
            player.SetActive(true);
            yield return null;
            Assert.IsTrue(witch.isActiveAndEnabled);
            Assert.AreEqual(WitchForm.Flying, witch.Decision.Form);
            Assert.AreEqual(0, heartbeat.Beats);
            yield return new WaitForSeconds(2f);
            Vector3 offset = witch.transform.position - player.transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 0.01f);
            Assert.IsTrue(witch.isActiveAndEnabled);
            Assert.AreEqual(WitchForm.Flying, witch.Decision.Form);
            Assert.AreEqual(0f, panic.Level);
            Assert.AreEqual(0, heartbeat.Beats);
        }

        [UnityTest]
        public IEnumerator PanicThresholdProducesImmediateBeatWithDistantWitch()
        {
            witch.transform.position = new Vector3(40f, 100f, 0f);
            player.SetActive(true);
            yield return null;
            Assert.IsTrue(light.State.Visible);
            Assert.AreEqual(0, heartbeat.Beats);
            panic.Model.ResetTo(HeartbeatRate.Audible);
            yield return null;
            Assert.AreEqual(1, heartbeat.Beats);
            Assert.AreSame(heartbeat.clip, heartbeat.source.clip);
            Assert.AreEqual(HeartbeatRate.MinVolume, heartbeat.source.volume, 1e-4f);
        }

        [UnityTest]
        public IEnumerator CaptureRestoresOnlyTheWitchItDeactivated()
        {
            var stage = player.AddComponent<WitchAwayStage>();
            stage.Apply();
            Assert.IsFalse(witch.gameObject.activeSelf);
            stage.Apply();
            stage.Clear();
            Assert.IsTrue(witch.gameObject.activeSelf);
            witch.gameObject.SetActive(false);
            stage.Apply();
            stage.Clear();
            Assert.IsFalse(witch.gameObject.activeSelf);
            yield return null;
        }
    }
}
