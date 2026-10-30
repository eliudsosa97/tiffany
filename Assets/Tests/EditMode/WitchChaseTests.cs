using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchChaseTests
    {
        private WitchProfile profile;
        private WitchBrain brain;
        private WitchPerception p;

        [SetUp]
        public void SetUp()
        {
            profile = new WitchProfile();
            brain = new WitchBrain(profile, null);
            p = new WitchPerception { PlayerPosition = Vector3.right * 8f, Raining = true };
            Assert.AreEqual(WitchState.Landing, WitchSim.Step(brain, ref p, 0f).State);
            Step(1.51f, WitchState.Stalking, voice: true);
            p.Raining = false;
            p.SmellsPlayer = true;
        }

        [Test]
        public void SmellTriggersStationaryWarningThenSprint()
        {
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 6f;
            Assert.AreEqual(p.PlayerPosition, Step(0f, WitchState.WindUp).MoveTarget);
            Step(0.79f, WitchState.WindUp);
            Assert.AreEqual(Vector3.zero, p.WitchPosition);
            p.PlayerPosition = Vector3.forward * 8f + Vector3.up * 15f;
            WitchDecision sprint = Step(0.02f, WitchState.Sprinting, 9f);
            Assert.That(Vector3.Distance(Vector3.forward * 10.8f, sprint.MoveTarget), Is.LessThan(1e-4f));
        }

        [TestCase(PlayerMotion.WalkSpeed)]
        [TestCase(PlayerMotion.RunSpeed)]
        public void SprintKeepsItsEndpointAndMissesLateralMovement(float playerSpeed)
        {
            Vector3 endpoint = Sprint().MoveTarget;
            for (int i = 0; i < 59; i++)
            {
                p.PlayerPosition += Vector3.forward * (playerSpeed * 0.02f);
                Assert.AreEqual(endpoint, Step(0.02f, WitchState.Sprinting, 9f).MoveTarget);
            }
            p.PlayerPosition += Vector3.forward * (playerSpeed * 0.01f);
            Assert.AreEqual(endpoint, Step(0.01f, WitchState.Sprinting, 9f).MoveTarget);
            Assert.Greater(p.WitchPosition.x, p.PlayerPosition.x);
            p.PlayerPosition += Vector3.forward * (playerSpeed * 0.02f);
            Step(0.02f, WitchState.Recovering);
        }

        [Test]
        public void SprintCatchesStationaryPlayerAndCaughtIsFinal()
        {
            WitchDecision decision = Sprint();
            p.SmellsPlayer = false;
            for (int i = 0; i < 119 && decision.State != WitchState.Caught; i++)
                decision = WitchSim.Step(brain, ref p, 0.01f);
            Step(0f, WitchState.Caught);
            Assert.Less(p.DistanceToPlayer, 1.2f);
            p.Flashed = p.PlayerVanished = true;
            p.WitchPosition = Vector3.one * 20f;
            Assert.AreEqual(p.WitchPosition, Step(10f, WitchState.Caught).MoveTarget);
        }

        [Test]
        public void RecoveryPausesThenFollowsWithoutVoiceUntilArrival()
        {
            Recover();
            Vector3 anchor = p.WitchPosition;
            Assert.AreEqual(anchor, Step(0.79f, WitchState.Recovering).MoveTarget);
            Assert.AreEqual(anchor, p.WitchPosition);
            Assert.AreEqual(p.PlayerPosition, Step(0.02f, WitchState.Stalking, profile.FollowSpeed).MoveTarget);
            Step(0f, WitchState.Stalking, profile.FollowSpeed);
            p.SmellsPlayer = false;
            p.WitchPosition = p.PlayerPosition;
            Step(0f, WitchState.Stalking, voice: true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LandingAfterChaseRestoresSlowStalkingAndVoice(bool raining)
        {
            Recover();
            Step(0.81f, WitchState.Stalking, profile.FollowSpeed);
            p.SmellsPlayer = false;
            Step(7.99f, WitchState.Stalking, profile.FollowSpeed);
            Step(0.02f, WitchState.TakingOff);
            p.Raining = raining;
            if (!raining)
            {
                Step(1.51f, WitchState.Flying, 6f);
                p.SeesPlayerLight = true;
                p.PlayerLightPosition = p.WitchPosition;
            }
            Assert.AreEqual(WitchState.Landing, WitchSim.Step(brain, ref p, 0f).State);
            Assert.AreEqual(WitchState.Stalking, WitchSim.Step(brain, ref p, 1.51f).State);
            p.SeesPlayerLight = false;
            p.HeardNoise = true;
            p.NoisePosition = p.WitchPosition + Vector3.forward * 20f;
            Step(0f, WitchState.Suspicious);
            p.HeardNoise = false;
            Step(profile.SuspicionDuration, WitchState.Stalking, 1.2f, true);
            Assert.AreEqual(p.NoisePosition, Step(0f, WitchState.Stalking, 1.2f, true).MoveTarget);
        }

        [Test]
        public void StalkingWarnsBeforeCloseCaptureButFollowingStillCatches()
        {
            p.PlayerPosition = Vector3.right * 0.5f;
            Step(0f, WitchState.WindUp);
            Recover();
            Step(0.81f, WitchState.Stalking, profile.FollowSpeed);
            p.PlayerPosition = p.WitchPosition + Vector3.right * 0.5f;
            Step(0f, WitchState.Caught);
        }

        [Test]
        public void DisabledSprintStalksSlowlyAndStillCatches()
        {
            profile.CanSprint = false;
            WitchDecision decision = Step(0.5f, WitchState.Stalking, 1.2f, true);
            Assert.AreEqual(0.6f, p.WitchPosition.x, 1e-4f);
            for (int i = 0; i < 140 && decision.State != WitchState.Caught; i++)
            {
                decision = WitchSim.Step(brain, ref p, 0.05f);
                Assert.That(decision.State, Is.EqualTo(WitchState.Stalking).Or.EqualTo(WitchState.Caught));
            }
            Step(0f, WitchState.Caught);
            Assert.Less(p.DistanceToPlayer, 1.2f);
        }

        [TestCase(0f, true)]
        [TestCase(1.4f, true)]
        [TestCase(3.5f, false)]
        [TestCase(-3.5f, false)]
        public void StalkingCatchNeedsTheSameFloor(float playerHeight, bool caught)
        {
            profile.CanSprint = false;
            p.PlayerPosition = p.WitchPosition + Vector3.up * playerHeight;
            Assert.AreEqual(caught ? WitchState.Caught : WitchState.Stalking, WitchSim.Step(brain, ref p, 0f).State);
            Assert.AreEqual(0f, p.DistanceToPlayer, 1e-4f);
        }

        [TestCase(0f, true)]
        [TestCase(3.5f, false)]
        public void SprintCatchNeedsTheSameFloor(float playerHeight, bool caught)
        {
            Sprint();
            p.SmellsPlayer = false;
            p.WitchPosition = new Vector3(p.PlayerPosition.x, p.PlayerPosition.y - playerHeight, p.PlayerPosition.z);
            Assert.AreEqual(caught ? WitchState.Caught : WitchState.Sprinting, WitchSim.Step(brain, ref p, 0f).State);
        }

        [Test]
        public void FlashInterruptsWarningRestartsStunAndIsIgnoredInFlight()
        {
            Step(0f, WitchState.WindUp);
            p.Flashed = true;
            Step(0.1f, WitchState.Stunned);
            p.Flashed = false;
            Step(1.99f, WitchState.Stunned);
            p.Flashed = true;
            Step(0.02f, WitchState.Stunned);
            p.Flashed = false;
            Step(1.99f, WitchState.Stunned);
            Step(0.02f, WitchState.Stalking, 1.2f, true);
            brain = new WitchBrain(profile, null);
            p.Flashed = true;
            Step(1f, WitchState.Flying, 6f);
        }

        [Test]
        public void FlashStopsSprintAndPreventsCaptureWhileStunned()
        {
            Sprint();
            Step(0.4f, WitchState.Sprinting, 9f);
            Vector3 anchor = p.WitchPosition;
            p.PlayerPosition = anchor + Vector3.right * 0.5f;
            p.Flashed = true;
            Assert.AreEqual(anchor, Step(0.01f, WitchState.Stunned).MoveTarget);
            p.Flashed = false;
            Step(1.99f, WitchState.Stunned);
            Assert.AreEqual(anchor, p.WitchPosition);
        }

        [Test]
        public void SafeZonePreventsWarningAndCapture()
        {
            p.PlayerInSafeZone = true;
            p.PlayerPosition = Vector3.right * 5f;
            Step(0f, WitchState.Stalking, voice: true);
            p.PlayerPosition = Vector3.right * 0.5f;
            Step(0f, WitchState.Stalking, voice: true);
            Assert.AreEqual(Vector3.zero, p.WitchPosition);
        }

        [Test]
        public void SafeZoneEndsChaseAndRequiresWarningOnCloseExit()
        {
            Vector3 target = p.PlayerPosition;
            Step(0f, WitchState.WindUp);
            p.PlayerInSafeZone = true;
            Assert.AreEqual(target, Step(0.1f, WitchState.Stalking, 1.2f, true).MoveTarget);
            p.PlayerPosition = p.WitchPosition + Vector3.right * 0.5f;
            Assert.AreEqual(target, Step(0.1f, WitchState.Stalking, 1.2f, true).MoveTarget);
            Assert.Greater(Vector3.Distance(p.WitchPosition, target), profile.ArriveDistance);
            Assert.Less(p.DistanceToPlayer, profile.CatchDistance);
            p.PlayerInSafeZone = false;
            Step(0f, WitchState.WindUp);
        }

        [Test]
        public void VanishingStopsFollowingAndPreventsSprintCapture()
        {
            Recover();
            Step(0.81f, WitchState.Stalking, profile.FollowSpeed);
            p.PlayerVanished = true;
            Assert.AreEqual(p.WitchPosition, Step(0f, WitchState.Stalking, voice: true).MoveTarget);
            SetUp();
            Sprint();
            Step(0.4f, WitchState.Sprinting, 9f);
            p.PlayerVanished = true;
            p.PlayerPosition = p.WitchPosition + Vector3.right * 0.5f;
            Step(0.01f, WitchState.Sprinting, 9f);
            Step(0.8f, WitchState.Recovering);
        }

        [Test]
        public void HidingOnlyPreservesTrackingWhenEntryWasSeen()
        {
            p.SeesPlayer = true;
            p.PlayerPosition = Vector3.right * 12f;
            Step(0f, WitchState.Stalking, 1.2f, true);
            p.PlayerHidden = true;
            p.SeesPlayer = p.SmellsPlayer = false;
            p.PlayerPosition = Vector3.right * 8f;
            Sprint();
            SetUp();
            Step(0f, WitchState.WindUp);
            p.PlayerHidden = p.SeesPlayer = true;
            Assert.AreEqual(p.WitchPosition, Step(0f, WitchState.Stalking, voice: true).MoveTarget);
            Step(0.81f, WitchState.Stalking, voice: true);
        }

        [Test]
        public void VanishingInsideSeenHidingSpotClearsTrackingAfterReappearing()
        {
            p.SeesPlayer = true;
            p.PlayerPosition = Vector3.right * 12f;
            Step(0f, WitchState.Stalking, 1.2f, true);
            p.PlayerHidden = true;
            p.SeesPlayer = p.SmellsPlayer = false;
            Step(0f, WitchState.Stalking, 1.2f, true);
            p.PlayerPosition = Vector3.right * 8f;
            p.PlayerVanished = true;
            Step(0f, WitchState.Stalking, voice: true);
            Step(2.99f, WitchState.Stalking, voice: true);
            p.PlayerVanished = false;
            Assert.AreEqual(p.WitchPosition, Step(0.01f, WitchState.Stalking, voice: true).MoveTarget);
        }

        [Test]
        public void HidingWithoutBeingSeenPreservesDecoyTarget()
        {
            p.SmellsPlayer = false;
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 24f;
            Vector3 decoy = p.NoisePosition;
            Assert.AreEqual(decoy, Step(1f, WitchState.Suspicious).MoveTarget);
            p.HeardNoise = false;
            p.PlayerHidden = true;
            Step(profile.SuspicionDuration, WitchState.Stalking, 1.2f, true);
            Assert.AreEqual(decoy, Step(1f, WitchState.Stalking, 1.2f, true).MoveTarget);
        }

        [Test]
        public void DecoyOnlyAttractsStalkingButAudiblePlayerNoiseTriggersWarning()
        {
            p.SmellsPlayer = false;
            p.HeardNoise = true;
            p.NoisePosition = p.PlayerPosition + Vector3.forward * 6f;
            Vector3 decoy = p.NoisePosition;
            Assert.AreEqual(decoy, Step(0f, WitchState.Suspicious).MoveTarget);
            p.HeardNoise = false;
            Step(profile.SuspicionDuration, WitchState.Stalking, 1.2f, true);
            p.PlayerVanished = true;
            Assert.AreEqual(decoy, Step(0f, WitchState.Stalking, 1.2f, true).MoveTarget);
            p.PlayerVanished = false;
            p.HeardNoise = true;
            p.HearingMuted = true;
            p.NoisePosition = p.PlayerPosition + Vector3.up * 20f;
            Assert.AreEqual(decoy, Step(0f, WitchState.Stalking, 1.2f, true).MoveTarget);
            p.HearingMuted = false;
            Assert.AreEqual(p.PlayerPosition, Step(0f, WitchState.WindUp).MoveTarget);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void VanishedPlayerNoiseDoesNotRedirectOrDelayTakeOffButDecoyDoes(float noiseOffset)
        {
            p.SmellsPlayer = false;
            p.PlayerVanished = true;
            Step(0f, WitchState.Stalking, voice: true);
            Step(7.9f, WitchState.Stalking, voice: true);
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 40f;
            Vector3 decoy = p.NoisePosition;
            Assert.AreEqual(decoy, Step(0.2f, WitchState.Suspicious).MoveTarget);
            p.HeardNoise = false;
            Step(profile.SuspicionDuration, WitchState.Stalking, 1.2f, true);
            p.HeardNoise = true;
            p.NoisePosition = p.PlayerPosition + Vector3.right * noiseOffset;
            Assert.AreEqual(decoy, Step(7.9f, WitchState.Stalking, 1.2f, true).MoveTarget);
            p.WitchPosition = decoy;
            Step(0f, WitchState.Stalking, voice: true);
            Step(profile.HideInspectionDuration - 0.1f, WitchState.Stalking, voice: true);
            Step(0.11f, WitchState.TakingOff);
        }

        [Test]
        public void EightSecondsWithoutPerceptionAfterLosingPlayerTriggersTakeOff()
        {
            Recover();
            Step(0.81f, WitchState.Stalking, profile.FollowSpeed);
            p.PlayerVanished = true;
            Step(0f, WitchState.Stalking, voice: true);
            Step(7.99f, WitchState.Stalking, voice: true);
            Step(0.02f, WitchState.TakingOff);
        }

        private WitchDecision Sprint()
        {
            Step(0f, WitchState.WindUp);
            Step(0.79f, WitchState.WindUp);
            return Step(0.02f, WitchState.Sprinting, 9f);
        }

        private void Recover()
        {
            Sprint();
            p.PlayerPosition = Vector3.right * 40f;
            Step(1.19f, WitchState.Sprinting, 9f);
            Step(0.02f, WitchState.Recovering);
        }

        private WitchDecision Step(float dt, WitchState state, float speed = 0f, bool voice = false)
        {
            WitchDecision decision = WitchSim.Step(brain, ref p, dt);
            Assert.AreEqual(state, decision.State);
            bool flying = state == WitchState.Flying || state == WitchState.TakingOff;
            Assert.AreEqual(flying ? WitchForm.Flying : WitchForm.Grounded, decision.Form);
            Assert.AreEqual(flying, decision.FireLit);
            Assert.AreEqual(voice, decision.VoiceLure);
            Assert.AreEqual(speed, decision.Speed, 1e-4f);
            if (state == WitchState.Caught || state == WitchState.Recovering || state == WitchState.Stunned)
                Assert.AreEqual(p.WitchPosition, decision.MoveTarget);
            return decision;
        }
    }
}
