using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PanicControllerTests
    {
        private PanicController panic;
        private int safeZone;
        private bool heardSob;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            panic = Object.FindAnyObjectByType<PanicController>();
            Assert.IsNotNull(panic);
            Assert.IsNotNull(panic.hiding);
            panic.GetComponent<PlayerController>().enabled = false;
            panic.witch.gameObject.SetActive(false);
            panic.witch.transform.position = Vector3.one * 1000f;
            panic.tyler.enabled = false;
            panic.tyler.transform.position = Vector3.one * 1000f;
            yield return null;
            Assert.IsFalse(SafeZones.Contains(panic.transform.position));
            heardSob = false;
            NoiseBus.Published += Hear;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= Hear;
            SafeZones.Remove(safeZone);
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(PanicControllerTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator ChildrenPublishSobAndShakeLightWithoutRecordedSob()
        {
            Assert.AreEqual(0f, panic.cueSource.spatialBlend);
            Assert.IsFalse(panic.cueSource.playOnAwake);
            Assert.AreEqual(4, panic.fakeSteps.Length);
            Assert.AreEqual(3, panic.whispers.Length);
            CollectionAssert.AllItemsAreNotNull(panic.fakeSteps);
            CollectionAssert.AllItemsAreNotNull(panic.whispers);
            Assert.IsNull(panic.sob);
            panic.Model.ResetTo(0.95f);
            if (!panic.playerLight.State.IsOn) panic.playerLight.Toggle();
            bool flickered = false;
            float deadline = Time.time + 5f;
            while (Time.time < deadline && !(heardSob && flickered))
            {
                yield return null;
                flickered |= panic.playerLight.intensityScale < 1f;
            }
            Assert.IsTrue(heardSob && flickered);
            panic.enabled = false;
            Assert.AreEqual(1f, panic.playerLight.intensityScale);
        }

        [UnityTest]
        public IEnumerator DarknessRaisesPanicAndSafeLightWithTylerCalmsIt()
        {
            panic.Model.ResetTo(0.4f);
            if (panic.playerLight.State.IsOn) panic.playerLight.Toggle();
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(panic.Level, 0.4f);
            float before = panic.Level;
            safeZone = SafeZones.Add(panic.transform.position, 3f);
            panic.tyler.transform.position = panic.transform.position + Vector3.right;
            panic.tyler.nextMemory = null;
            panic.tyler.enabled = true;
            panic.playerLight.Toggle();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(panic.tyler.NearPlayer);
            Assert.Less(panic.Level, before);
        }

        private void Hear(StepNoise noise) => heardSob |= noise.Radius == PanicEffects.SobRadius
            && Vector3.Distance(noise.Position, panic.transform.position) < 0.01f;
    }
}
