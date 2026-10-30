using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchBrainTests
    {
        private WitchProfile profile;
        private Vector3[] route;
        private WitchBrain brain;
        private WitchPerception p;

        [SetUp]
        public void SetUp()
        {
            profile = new WitchProfile();
            route = new[] { new Vector3(0f, 20f, 0f), new Vector3(18f, 20f, 0f), new Vector3(18f, 20f, 18f) };
            brain = new WitchBrain(profile, route);
            p = new WitchPerception
            {
                WitchPosition = new Vector3(0f, 10f, 0f),
                PlayerLightPosition = new Vector3(60f, 0f, 0f),
                PlayerPosition = new Vector3(60f, 0f, 0f),
                NoisePosition = new Vector3(24f, 0f, 0f)
            };
        }

        [Test]
        public void FlyingCyclesThroughCopiedRouteWithFire()
        {
            Vector3 first = route[0];
            route[0] = Vector3.one * 100f;
            Check(Step(2f), WitchState.Flying, first, 6f);
            Check(Step(3f), WitchState.Flying, route[1], 6f);
            Check(Step(3f), WitchState.Flying, route[2], 6f);
            Check(Step(5f), WitchState.Flying, first, 6f);
            Check(Step(0f), WitchState.Flying, route[1], 6f);
            Assert.Throws<ArgumentNullException>(() => new WitchBrain(null, route));
        }

        [Test]
        public void FlyingIgnoresNoiseAndGroundSensesAndAcceptsMissingRoutes()
        {
            p.HeardNoise = p.SmellsPlayer = p.Flashed = true;
            p.NoisePosition = p.WitchPosition;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
            brain = new WitchBrain(profile, null);
            Check(Step(2f), WitchState.Flying, p.WitchPosition, 6f);
            brain = new WitchBrain(profile, Array.Empty<Vector3>());
            Check(Step(2f), WitchState.Flying, p.WitchPosition, 6f);
        }

        [Test]
        public void FlyingFireDetectsOnlyExposedPlayerAndFollowsLandingRules()
        {
            p.WitchPosition = route[0];
            Check(Step(0f), WitchState.Flying, route[1], 6f);
            p.SeesPlayer = p.PlayerHidden = true;
            Check(Step(0f), WitchState.Flying, route[1], 6f);
            p.PlayerHidden = false;
            p.PlayerVanished = true;
            Check(Step(0f), WitchState.Flying, route[1], 6f);
            p.PlayerVanished = false;
            Vector3 abovePlayer = new Vector3(60f, 20f, 0f);
            Check(Step(0f), WitchState.Flying, abovePlayer, 6f);

            p.SeesPlayerLight = true;
            p.PlayerLightPosition = new Vector3(90f, 0f, 30f);
            Check(Step(0f), WitchState.Flying, new Vector3(90f, 20f, 30f), 6f);
            profile.CanLand = false;
            p.PlayerLightPosition = p.PlayerPosition;
            p.WitchPosition = abovePlayer;
            Check(Step(0f), WitchState.Flying, abovePlayer, 6f);
            Check(Step(1f), WitchState.Flying, abovePlayer, 6f);
            profile.CanLand = true;
            p.SeesPlayerLight = false;
            p.PlayerInSafeZone = true;
            Check(Step(0f), WitchState.Flying, route[1], 6f);

            p.PlayerInSafeZone = false;
            p.WitchPosition = abovePlayer + Vector3.left * 12.01f;
            Check(Step(0f), WitchState.Flying, abovePlayer, 6f);
            p.WitchPosition = abovePlayer + Vector3.left * 12f;
            Check(Step(0f), WitchState.Landing, p.WitchPosition);
            Check(Step(1.51f), WitchState.Stalking, p.PlayerPosition, 0.9f);
        }

        [Test]
        public void FlyingLightUsesCurrentRouteAltitudeOrCurrentHeightWithoutRoute()
        {
            route[1] = new Vector3(18f, 35f, 0f);
            brain = new WitchBrain(profile, route);
            p.WitchPosition = route[0];
            Check(Step(0f), WitchState.Flying, route[1], 6f);
            p.WitchPosition.y = 2f;
            ConfirmDistantLight();
            Check(Step(0f), WitchState.Flying, new Vector3(60f, 35f, 0f), 6f);

            brain = new WitchBrain(profile, null);
            ConfirmDistantLight();
            Check(Step(0f), WitchState.Flying, new Vector3(60f, 2f, 0f), 6f);
            brain = new WitchBrain(profile, Array.Empty<Vector3>());
            ConfirmDistantLight();
            Check(Step(0f), WitchState.Flying, new Vector3(60f, 2f, 0f), 6f);
        }

        [Test]
        public void LightLeadsToFixedLandingThenStalking()
        {
            ConfirmDistantLight();
            Vector3 light = p.PlayerLightPosition;
            Check(Step(9f), WitchState.Flying, new Vector3(60f, 20f, 0f), 6f);
            Vector3 anchor = p.WitchPosition;
            Check(Step(4f), WitchState.Landing, anchor);
            p.PlayerLightPosition = Vector3.left * 100f;
            p.HeardNoise = p.Flashed = true;
            p.WitchPosition.y = 5f;
            Check(Step(-1f), WitchState.Landing, anchor);
            Check(Step(float.NaN), WitchState.Landing, anchor);
            Check(Step(1.49f), WitchState.Landing, anchor);
            p.WitchPosition.y = 0f;
            Check(Step(0.02f), WitchState.Stalking, light, 0.9f);
        }

        [Test]
        public void RememberedLightStillCausesLandingAfterItIsSwitchedOff()
        {
            ConfirmDistantLight();
            Step(1f);
            p.PlayerLightPosition = new Vector3(90f, 0f, 0f);
            Check(Step(13f), WitchState.Flying, new Vector3(90f, 20f, 0f), 6f);
            Vector3 light = p.PlayerLightPosition;
            p.SeesPlayerLight = false;
            p.PlayerLightPosition = Vector3.zero;
            Check(Step(0f), WitchState.Landing, p.WitchPosition);
            Check(Step(1.51f), WitchState.Stalking, light, 0.9f);
        }

        [Test]
        public void DisabledLandingHoversAndResumesRouteAfterLosingLight()
        {
            profile.CanLand = false;
            ConfirmDistantLight();
            Vector3 aboveLight = new Vector3(60f, 20f, 0f);
            Check(Step(2f), WitchState.Flying, aboveLight, 6f);
            p.SeesPlayerLight = false;
            Check(Step(8f), WitchState.Flying, aboveLight, 6f);
            Check(Step(0f), WitchState.Flying, route[0], 6f);
            p.SeesPlayerLight = true;
            Check(Step(20f), WitchState.Flying, aboveLight, 6f);
            p.SeesPlayerLight = false;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
        }

        [Test]
        public void SafeZonePreventsLanding()
        {
            p.PlayerInSafeZone = true;
            ConfirmDistantLight();
            Vector3 aboveLight = new Vector3(60f, 20f, 0f);
            Check(Step(10f), WitchState.Flying, aboveLight, 6f);
            Check(Step(20f), WitchState.Flying, aboveLight, 6f);
            p.SeesPlayerLight = false;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
        }

        [Test]
        public void FlyingResumesRouteWhenVisiblePlayerEntersSafeZone()
        {
            p.SeesPlayer = true;
            Vector3 abovePlayer = new Vector3(60f, 20f, 0f);
            Check(Step(0f), WitchState.Flying, abovePlayer, 6f);
            p.PlayerInSafeZone = true;
            p.WitchPosition = abovePlayer;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
            p.WitchPosition = route[0];
            Check(Step(0f), WitchState.Flying, route[1], 6f);
        }

        [Test]
        public void StalkingFollowsLastAudibleNoiseAndStopsOnHorizontalArrival()
        {
            Ground();
            p.HeardNoise = true;
            Vector3 noise = p.NoisePosition;
            Assert.AreEqual(WitchState.Suspicious, Step(1f).State);
            p.HeardNoise = false;
            Check(Step(profile.SuspicionDuration), WitchState.Stalking, noise, 0.9f);
            Check(Step(1f), WitchState.Stalking, noise, 0.9f);
            p.HeardNoise = p.HearingMuted = true;
            p.NoisePosition = Vector3.right * 48f;
            Check(Step(1f), WitchState.Stalking, noise, 0.9f);
            p.WitchPosition = new Vector3(23f, 100f, 0f);
            Check(Step(0f), WitchState.Stalking, noise);
            profile.UsesVoiceLure = false;
            Check(Step(profile.HideInspectionDuration - 0.1f), WitchState.Stalking, noise);
            Check(Step(0.11f), WitchState.TakingOff, p.WitchPosition);
        }

        [Test]
        public void SilenceTriggersFixedTakeOffAndResumesAtNearestRoutePointIn3D()
        {
            route[0] = new Vector3(18f, 100f, 18f);
            brain = new WitchBrain(profile, route);
            Ground();
            p.WitchPosition = new Vector3(18f, 0f, 18f);
            Check(Step(7.9f), WitchState.Stalking, p.WitchPosition);
            Vector3 anchor = p.WitchPosition;
            Check(Step(0.2f), WitchState.TakingOff, anchor);
            p.HeardNoise = p.SeesPlayerLight = p.SeesPlayer = p.SmellsPlayer = p.Flashed = true;
            p.WitchPosition = route[2];
            Check(Step(1.49f), WitchState.TakingOff, anchor);
            Check(Step(0.02f), WitchState.Flying, route[2], 6f);
        }

        [Test]
        public void AudibleNoiseRestartsTheSilenceTimer()
        {
            Ground();
            Check(Step(7.9f), WitchState.Stalking, p.WitchPosition);
            p.HeardNoise = true;
            Assert.AreEqual(WitchState.Suspicious, Step(20f).State);
            p.HeardNoise = false;
            Check(Step(profile.SuspicionDuration), WitchState.Stalking, p.NoisePosition, 0.9f);
            p.WitchPosition = p.NoisePosition;
            Check(Step(7.9f), WitchState.Stalking, p.WitchPosition);
            Check(Step(profile.HideInspectionDuration - 0.1f), WitchState.Stalking, p.WitchPosition);
            Check(Step(0.11f), WitchState.TakingOff, p.WitchPosition);
        }

        [Test]
        public void RainForcesLandingPreventsTakeOffAndInterruptsTakeOff()
        {
            p.PlayerInSafeZone = p.SeesPlayerLight = true;
            Step(1f);
            Ground();
            p.Raining = true;
            Check(Step(20f), WitchState.Stalking, p.WitchPosition);
            p.Raining = false;
            Check(Step(0f), WitchState.TakingOff, p.WitchPosition);
            p.WitchPosition.y = 7f;
            p.Raining = true;
            Vector3 anchor = p.WitchPosition;
            Check(Step(2f), WitchState.Landing, anchor);
            p.WitchPosition.y = 3f;
            Check(Step(1.49f), WitchState.Landing, anchor);
            Check(Step(0.02f), WitchState.Stalking, p.WitchPosition);
        }

        [Test]
        public void CemeteryFliesHigherAndOnlyFollowsLightEvenInRain()
        {
            profile = WitchProfiles.For(Chapter.Cemetery);
            brain = new WitchBrain(profile, route);
            Vector3 height = Vector3.up * profile.FlightHeightOffset;
            p.WitchPosition = route[0] + height;
            p.SeesPlayer = p.Raining = true;
            Check(Step(0f), WitchState.Flying, route[1] + height, 6f);
            ConfirmDistantLight();
            Vector3 aboveLight = new Vector3(p.PlayerLightPosition.x, 30f, p.PlayerLightPosition.z);
            Check(Step(0f), WitchState.Flying, aboveLight, 6f);
            p.WitchPosition = aboveLight;
            Check(Step(2f), WitchState.Flying, aboveLight, 6f);
            p.SeesPlayerLight = false;
            Check(Step(0f), WitchState.Flying, route[1] + height, 6f);
        }

        [Test]
        public void CemeteryWithoutRouteKeepsItsHeight()
        {
            profile = WitchProfiles.For(Chapter.Cemetery);
            brain = new WitchBrain(profile, null);
            Vector3 start = p.WitchPosition;
            for (int i = 0; i < 3; i++) Check(Step(1f), WitchState.Flying, start, 6f);
        }

        [Test]
        public void ProfileChangeKeepsLandingTimerAndEnablesForestLure()
        {
            profile = WitchProfiles.For(Chapter.WaterPlant);
            brain = new WitchBrain(profile, route);
            p.Raining = true;
            Check(Step(0f), WitchState.Landing, p.WitchPosition);
            Check(Step(0.9f), WitchState.Landing, p.WitchPosition);
            profile = WitchProfiles.For(Chapter.TrainTracks);
            brain.SetProfile(profile);
            Check(Step(0.61f), WitchState.Stalking, p.WitchPosition);
        }

        [Test]
        public void ProfileChangeKeepsSprintTargetAndElapsedTime()
        {
            Ground();
            p.PlayerPosition = p.WitchPosition + Vector3.forward * 5f;
            p.SeesPlayer = true;
            Assert.AreEqual(WitchState.WindUp, WitchSim.BeginWarning(brain, profile, ref p).State,
                "El perfil cambia después del acoso y de su aviso completo.");
            Assert.AreEqual(WitchState.Sprinting, Step(0.81f).State);
            p.PlayerPosition = Vector3.left * 30f;
            WitchDecision sprint = Step(0.4f);
            brain.SetProfile(WitchProfiles.For(Chapter.WitchHut));
            WitchDecision continued = Step(0.79f);
            Assert.AreEqual(WitchState.Sprinting, continued.State);
            Assert.AreEqual(sprint.MoveTarget, continued.MoveTarget);
            Assert.AreEqual(sprint.Speed, continued.Speed);
            Assert.AreEqual(WitchState.Recovering, Step(0.02f).State);
        }

        private WitchDecision Step(float dt) => WitchSim.Step(brain, ref p, dt);

        private void ConfirmDistantLight()
        {
            p.SeesPlayerLight = true;
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            p.SeesPlayerLight = false;
            Step(0f);
            p.SeesPlayerLight = true;
            Assert.AreEqual(WitchState.Flying, Step(0f).State);
        }

        private void Ground()
        {
            p.Raining = true;
            Check(Step(20f), WitchState.Landing, p.WitchPosition);
            Check(Step(1.51f), WitchState.Stalking, p.WitchPosition);
            p.Raining = false;
            p.WitchPosition.y = 0f;
        }

        private void Check(WitchDecision decision, WitchState state, Vector3 target, float speed = 0f)
        {
            Assert.AreEqual(state, decision.State);
            Assert.AreEqual(state == WitchState.Stalking ? WitchForm.Grounded : WitchForm.Flying, decision.Form);
            Assert.AreEqual(state == WitchState.Flying || state == WitchState.TakingOff, decision.FireLit);
            Assert.AreEqual(state == WitchState.Stalking && profile.UsesVoiceLure && !profile.BarkLureEnabled,
                decision.VoiceLure, "Con el ladrido del bosque habilitado, aterrizar ya no enciende la voz continua.");
            Assert.AreEqual(speed, decision.Speed, 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(target, decision.MoveTarget), 1e-4f);
        }
    }
}
