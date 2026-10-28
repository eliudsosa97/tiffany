using System;
using NUnit.Framework;
using UnityEngine;

namespace ElVelador.Tests
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
            p.HeardNoise = p.SmellsPlayer = p.SeesPlayer = p.Flashed = true;
            p.NoisePosition = p.WitchPosition;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
            brain = new WitchBrain(profile, null);
            Check(Step(2f), WitchState.Flying, p.WitchPosition, 6f);
            brain = new WitchBrain(profile, Array.Empty<Vector3>());
            Check(Step(2f), WitchState.Flying, p.WitchPosition, 6f);
        }

        [Test]
        public void LightLeadsToFixedLandingThenStalking()
        {
            p.SeesPlayerLight = true;
            Vector3 light = p.PlayerLightPosition;
            Check(Step(8f), WitchState.Flying, new Vector3(60f, 10f, 0f), 6f);
            Vector3 anchor = p.WitchPosition;
            Check(Step(4f), WitchState.Landing, anchor);
            p.PlayerLightPosition = Vector3.left * 100f;
            p.HeardNoise = p.Flashed = true;
            p.WitchPosition.y = 5f;
            Check(Step(-1f), WitchState.Landing, anchor);
            Check(Step(float.NaN), WitchState.Landing, anchor);
            Check(Step(1.49f), WitchState.Landing, anchor);
            p.WitchPosition.y = 0f;
            Check(Step(0.02f), WitchState.Stalking, light, 1.2f);
        }

        [Test]
        public void RememberedLightStillCausesLandingAfterItIsSwitchedOff()
        {
            p.SeesPlayerLight = true;
            Step(1f);
            p.PlayerLightPosition = new Vector3(90f, 0f, 0f);
            Check(Step(12f), WitchState.Flying, new Vector3(90f, 10f, 0f), 6f);
            Vector3 light = p.PlayerLightPosition;
            p.SeesPlayerLight = false;
            p.PlayerLightPosition = Vector3.zero;
            Check(Step(0f), WitchState.Landing, p.WitchPosition);
            Check(Step(1.51f), WitchState.Stalking, light, 1.2f);
        }

        [Test]
        public void DisabledLandingHoversAndResumesRouteAfterLosingLight()
        {
            profile.CanLand = false;
            p.SeesPlayerLight = true;
            Vector3 aboveLight = new Vector3(60f, 10f, 0f);
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
            p.PlayerInSafeZone = p.SeesPlayerLight = true;
            Vector3 aboveLight = new Vector3(60f, 10f, 0f);
            Check(Step(10f), WitchState.Flying, aboveLight, 6f);
            Check(Step(20f), WitchState.Flying, aboveLight, 6f);
            p.SeesPlayerLight = false;
            Check(Step(0f), WitchState.Flying, route[0], 6f);
        }

        [Test]
        public void StalkingFollowsLastAudibleNoiseAndStopsOnHorizontalArrival()
        {
            Ground();
            p.HeardNoise = true;
            Vector3 noise = p.NoisePosition;
            Check(Step(1f), WitchState.Stalking, noise, 1.2f);
            p.HeardNoise = false;
            Check(Step(1f), WitchState.Stalking, noise, 1.2f);
            p.HeardNoise = p.HearingMuted = true;
            p.NoisePosition = Vector3.right * 48f;
            Check(Step(1f), WitchState.Stalking, noise, 1.2f);
            p.WitchPosition = new Vector3(23f, 100f, 0f);
            Check(Step(0f), WitchState.Stalking, p.WitchPosition);
            profile.UsesVoiceLure = false;
            Check(Step(0f), WitchState.Stalking, p.WitchPosition);
            Check(Step(8.1f), WitchState.TakingOff, p.WitchPosition);
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
            Check(Step(20f), WitchState.Stalking, p.NoisePosition, 1.2f);
            p.HeardNoise = false;
            Check(Step(7.9f), WitchState.Stalking, p.WitchPosition);
            Check(Step(0.2f), WitchState.TakingOff, p.WitchPosition);
        }

        [Test]
        public void RainForcesLandingPreventsTakeOffAndInterruptsTakeOff()
        {
            profile.CanLand = false;
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

        private WitchDecision Step(float dt) => WitchSim.Step(brain, ref p, dt);

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
            Assert.AreEqual(state == WitchState.Stalking && profile.UsesVoiceLure, decision.VoiceLure);
            Assert.AreEqual(speed, decision.Speed, 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(target, decision.MoveTarget), 1e-4f);
        }
    }
}
