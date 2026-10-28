using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class TylerLightShareTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(TylerLightShareTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator SharesWithoutEnergyOrWitchSignalAndStopsAtFamilyCandle()
        {
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            Object.FindAnyObjectByType<GameInterface>().StartGame();
            var tyler = Object.FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            Assert.IsNotNull(tyler.soul);
            Assert.IsNotNull(tyler.shareLight);
            Assert.AreEqual(ShareMode.LightOnly, tyler.shareMode);
            Assert.IsFalse(tyler.shareLight.enabled);
            tyler.player.GetComponent<PlayerController>().enabled = false;
            tyler.witch.enabled = false;
            tyler.witch.GetComponent<NavMeshAgent>().enabled = false;
            tyler.witch.transform.position = Vector3.one * 1000f;
            tyler.nextMemory = null;
            Assert.IsFalse(SafeZones.Contains(tyler.player.position));
            tyler.soul.Energy.ResetTo(0f);
            tyler.Appear(tyler.player.position);
            yield return null;
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(tyler.NearPlayer && tyler.SharingLight && tyler.shareLight.enabled);
            Assert.AreEqual(0f, tyler.soul.Energy.Current);
            Assert.IsFalse(tyler.witch.playerLightOn);
            var panic = tyler.player.GetComponent<PanicController>();
            panic.Model.ResetTo(0.4f);
            float start = Time.time;
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0.4f - PanicModel.DogCalm * (Time.time - start), panic.Level, 0.001f);
            CandleSpot candle = null;
            foreach (var spot in Object.FindObjectsByType<CandleSpot>(FindObjectsSortMode.None))
                if (spot.Site.Candle.Kind == CandleKind.Family) { candle = spot; break; }
            Assert.IsNotNull(candle);
            var character = tyler.player.GetComponent<CharacterController>();
            character.enabled = false;
            tyler.player.position = candle.transform.position;
            character.enabled = true;
            tyler.Appear(tyler.player.position);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(tyler.NearPlayer && tyler.soul.RechargingAtCandle);
            Assert.Greater(tyler.soul.Energy.Current, 0f);
            Assert.IsFalse(tyler.SharingLight || tyler.shareLight.enabled);
            Assert.IsFalse(tyler.witch.playerLightOn);
        }
    }
}
