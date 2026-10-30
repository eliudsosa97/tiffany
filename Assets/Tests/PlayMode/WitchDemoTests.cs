using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class WitchDemoTests : TiffanyLogTests
    {
        WitchDemo demo;
        WitchController witch;
        int noises;
        int audibleNoises;
        Vector3 noiseOrigin;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.ClearOverrides();
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            witch = Object.FindAnyObjectByType<WitchController>();
            noises = audibleNoises = 0;
            NoiseBus.Published += CountNoise;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= CountNoise;
            if (demo != null) Object.Destroy(demo.gameObject);
            yield return null;
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(WitchDemoTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator ChaseDemoShowsBothCharactersAndWarnsAfterTwoNoises()
        {
            demo = new GameObject("WitchDemo Test").AddComponent<WitchDemo>();
            demo.chase = true;
            yield return WaitForState(WitchState.Suspicious, 5f);
            Assert.AreEqual(1, noises);
            CheckFraming(witch.transform.position);
            CheckFraming(witch.player.position);
            Vector3 anchor = witch.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(anchor, witch.transform.position);
            yield return WaitForState(WitchState.WindUp, witch.Profile.HarassTimeLimit + 3f);
            Assert.AreEqual(2, noises);
            anchor = witch.transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(WitchState.WindUp, witch.Decision.State);
            Assert.AreEqual(anchor, witch.transform.position);
            yield return WaitForState(WitchState.Sprinting, 1f);
        }

        [UnityTest]
        public IEnumerator NothingDemoInspectsEmptyOriginAndResumesFlightWithoutHunting()
        {
            float deadline = Time.time + WitchDemo.NothingDuration;
            demo = new GameObject("WitchDemo Test").AddComponent<WitchDemo>();
            yield return WaitForState(WitchState.Suspicious, 5f);
            CheckFraming(witch.transform.position);
            CheckFraming(witch.player.position);
            float inspectionTime = 0f;
            bool tookOff = false;
            bool resumedFlight = false;
            var player = witch.player.GetComponent<PlayerController>();
            Vector3 previousPosition = player.transform.position;
            while (Time.time < deadline)
            {
                WitchDecision decision = witch.Decision;
                Assert.AreNotEqual(WitchIntent.Harass, decision.Intent, "El origen vacío no debe iniciar el acoso.");
                Assert.That(decision.State, Is.EqualTo(WitchState.Suspicious).Or.EqualTo(WitchState.Stalking)
                    .Or.EqualTo(WitchState.TakingOff).Or.EqualTo(WitchState.Flying));
                Assert.AreEqual(1, noises);
                Assert.AreEqual(1, audibleNoises, "Los pasos al alejarse deben quedar fuera del oído de la bruja.");
                if (decision.State == WitchState.Stalking && decision.Speed == 0f
                    && PlaythroughPlan.HorizontalDistance(witch.transform.position, noiseOrigin) <= witch.Profile.ArriveDistance)
                    inspectionTime += Time.deltaTime;
                if (decision.State == WitchState.TakingOff)
                {
                    Assert.GreaterOrEqual(inspectionTime, witch.Profile.HideInspectionDuration - Time.deltaTime * 2f - 0.01f);
                    tookOff = true;
                }
                resumedFlight |= tookOff && decision.State == WitchState.Flying;
                if (PlaythroughPlan.HorizontalDistance(previousPosition, player.transform.position) > 0.001f)
                    Assert.AreEqual(Gait.Slow, player.CurrentGait);
                previousPosition = player.transform.position;
                yield return null;
            }
            Assert.IsTrue(tookOff && resumedFlight, "Debe revisar el origen y terminar el despegue durante el video.");
            Assert.Greater(PlaythroughPlan.HorizontalDistance(noiseOrigin, witch.player.position), 7f);
            Assert.IsFalse(witch.playerHidden || witch.playerVanished || witch.hearingMuted);
            CheckFraming(witch.player.position);
        }

        [Test]
        public void NothingVideoIncludesTimeForTheFullInvestigation()
        {
            Assert.AreEqual(WitchDemo.NothingDuration, VideoCapture.MinimumDuration(new[] { "-witchdemo", "nothing" }));
            Assert.AreEqual(WitchDemo.HarassDuration, VideoCapture.MinimumDuration(new[] { "-witchdemo", "harass" }),
                "El video de acoso incluye mirada, arco, escondite, aviso y embestida.");
            Assert.AreEqual(0f, VideoCapture.MinimumDuration(new[] { "-witchdemo", "chase" }));
            Assert.AreEqual(0f, VideoCapture.MinimumDuration(new[] { "-witchdemo" }));
        }

        [Test]
        public void GeneratedSceneHasSuspicionCaptureStage()
        {
            var stop = GameObject.Find("Witch Suspicious");
            Assert.IsNotNull(stop, "Regenera la escena antes de ejecutar PlayMode.");
            Assert.IsNotNull(stop.GetComponent<CaptureStop>());
            Assert.AreEqual(WitchState.Suspicious, stop.GetComponent<WitchCaptureStage>().state);
        }

        [UnityTest] public IEnumerator SearchDemoStartsWithinTwentyFiveSeconds() => CheckBehavior("search", WitchIntent.Search);
        [UnityTest] public IEnumerator AmbushDemoStartsWithinTwentyFiveSeconds() => CheckBehavior("ambush", WitchIntent.Ambush);
        [UnityTest] public IEnumerator CandleDemoStartsWithinTwentyFiveSeconds() => CheckBehavior("candle", WitchIntent.CandleWatch);
        [UnityTest] public IEnumerator BarkDemoStartsWithinTwentyFiveSeconds() => CheckBehavior("bark", WitchIntent.BarkLure);
        [UnityTest] public IEnumerator LightsOffDemoStartsWithinTwentyFiveSeconds() => CheckBehavior("lightsoff", WitchIntent.LastSeen);

        [UnityTest]
        public IEnumerator HarassDemoShowsLookingCirclingHidingAndWarningBeforeSprintWithinTwentyFiveSeconds()
        {
            demo = new GameObject("WitchDemo Test").AddComponent<WitchDemo>();
            demo.mode = "harass";
            float deadline = Time.time + 25f;
            bool looked = false, circled = false, hid = false, warned = false;
            bool lit = false;
            float lookingTime = 0f, hidingTime = 0f, warningTime = 0f;
            while (Time.time < deadline && witch.Decision.State != WitchState.Sprinting)
            {
                WitchDecision decision = witch.Decision;
                lit |= witch.Perception.PlayerLightTouchesWitch;
                if (decision.Intent == WitchIntent.Harass)
                {
                    if (decision.Speed > 0f) circled |= looked;
                    else if (circled && lit)
                    {
                        hid = true;
                        hidingTime += Time.deltaTime;
                    }
                    else if (!circled)
                    {
                        looked = true;
                        lookingTime += Time.deltaTime;
                    }
                }
                warned |= decision.State == WitchState.WindUp;
                if (decision.State == WitchState.WindUp) warningTime += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(looked && circled && hid && warned, "La demo debe mostrar las fases del acoso en orden.");
            Assert.GreaterOrEqual(lookingTime, witch.Profile.HarassMinLookDuration - Time.deltaTime * 3f);
            Assert.GreaterOrEqual(hidingTime, witch.Profile.HarassHideDuration - Time.deltaTime * 3f,
                "El refugio debe mostrar una pausa completa después de recibir la luz.");
            Assert.GreaterOrEqual(warningTime, witch.Profile.WindUpDuration - Time.deltaTime * 3f);
            Assert.AreEqual(WitchState.Sprinting, witch.Decision.State, "El aviso y la embestida caben en menos de 25 s.");
            CheckFraming(witch.transform.position);
            CheckFraming(witch.player.position);
        }

        private IEnumerator CheckBehavior(string mode, WitchIntent expected)
        {
            demo = new GameObject("WitchDemo Test").AddComponent<WitchDemo>();
            demo.mode = mode;
            float deadline = Time.time + 25f;
            while (witch.Decision.Intent != expected && Time.time < deadline) yield return null;
            Assert.AreEqual(expected, witch.Decision.Intent, "La demo debe disparar su conducta antes de 25 segundos.");
            if (mode == "ambush")
            {
                yield return WaitForState(WitchState.Stalking, 6f);
                Assert.AreEqual(0f, witch.Decision.Speed);
                Assert.IsFalse(witch.Decision.FireLit || witch.Decision.VoiceLure);
            }
            if (mode == "bark")
            {
                while (!witch.Decision.VoiceLure && Time.time < deadline) yield return null;
                Assert.IsTrue(witch.Decision.VoiceLure);
                Assert.AreEqual(0f, witch.Decision.Speed);
            }
        }

        void CountNoise(StepNoise noise)
        {
            if (Mathf.Approximately(noise.Radius, 20f))
            {
                noises++;
                noiseOrigin = noise.Position;
            }
            if (witch.Decision.Form == WitchForm.Grounded && !witch.hearingMuted
                && PlaythroughPlan.HorizontalDistance(witch.transform.position, noise.Position) <= noise.Radius)
                audibleNoises++;
        }

        static void CheckFraming(Vector3 position)
        {
            Vector3 point = Camera.main.WorldToViewportPoint(position + Vector3.up);
            Assert.Greater(point.z, 0f);
            Assert.That(point.x, Is.InRange(0.05f, 0.95f));
            Assert.That(point.y, Is.InRange(0.05f, 0.95f));
        }

        IEnumerator WaitForState(WitchState state, float seconds)
        {
            float deadline = Time.time + seconds;
            while (witch.Decision.State != state && Time.time < deadline) yield return null;
            Assert.AreEqual(state, witch.Decision.State);
        }
    }
}
