using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchWatchTests
    {
        [Test]
        public void GroundedWatchNeverTakesOffAndReturnsAfterInvestigating()
        {
            var brain = new WitchBrain(new WitchProfile(), null);
            brain.BeginWatch(Vector3.zero, false);
            var p = new WitchPerception { PlayerPosition = Vector3.back * 5f, DistanceToPlayer = 5f };
            var decision = brain.Tick(30f, in p);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(WitchForm.Grounded, decision.Form);
            Assert.IsFalse(decision.FireLit);
            p.HeardNoise = true;
            p.NoisePosition = Vector3.right * 6f;
            decision = brain.Tick(0.1f, in p);
            Assert.AreEqual(p.NoisePosition, decision.MoveTarget);
            Assert.IsFalse(brain.Watch.PassageClear(Vector3.zero));
            Assert.IsTrue(brain.Watch.PassageClear(Vector3.right * 4f));
            p.HeardNoise = false;
            brain.Tick(20f, in p);
            Assert.IsTrue(brain.Watch.Investigating, "La espera empieza al llegar al señuelo.");
            p.WitchPosition = p.NoisePosition;
            brain.Tick(WitchWatch.SearchSeconds - 0.1f, in p);
            Assert.IsTrue(brain.Watch.Investigating);
            decision = brain.Tick(0.2f, in p);
            Assert.IsFalse(brain.Watch.Investigating);
            Assert.AreEqual(Vector3.zero, decision.MoveTarget);
        }

        [TestCase(false, false, false, true)]
        [TestCase(false, true, false, false)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        public void WatchDetectsPassageUnlessItsRequiredPowerProtectsPlayer(bool requiresVanish, bool distracted,
            bool vanished, bool detected)
        {
            var brain = new WitchBrain(new WitchProfile(), null);
            brain.BeginWatch(Vector3.zero, requiresVanish);
            if (distracted) brain.Watch.Hear(Vector3.right * 6f);
            var p = new WitchPerception
            {
                WitchPosition = distracted ? Vector3.right * 4f : Vector3.zero,
                PlayerPosition = Vector3.forward, DistanceToPlayer = 4f,
                InWatchPassage = true, PlayerVanished = vanished
            };
            var decision = brain.Tick(0.1f, in p);
            Assert.AreEqual(detected ? p.PlayerPosition : brain.Watch.Target, decision.MoveTarget);
        }

        [Test]
        public void ThunderDoesNotConsumeTheWatchAndSafeZonesStillProtectPlayer()
        {
            var profile = new WitchProfile();
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, true);
            var p = new WitchPerception
            {
                PlayerPosition = Vector3.forward, DistanceToPlayer = 0.5f, InWatchPassage = true,
                PlayerInSafeZone = true, HearingMuted = true, HeardNoise = true, NoisePosition = Vector3.right * 6f
            };
            Assert.AreEqual(WitchState.Stalking, brain.Tick(0.1f, in p).State);
            Assert.IsFalse(brain.Watch.Investigating);
            p.HearingMuted = false;
            brain.Tick(0.1f, in p);
            Assert.IsTrue(brain.Watch.Investigating);
            p.PlayerInSafeZone = false;
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
            Assert.AreEqual(WitchState.Sprinting, brain.Tick(profile.WindUpDuration, in p).State);
            Assert.AreEqual(WitchState.Caught, brain.Tick(0.1f, in p).State);
        }

        [TestCase(Chapter.WaterPlant)]
        [TestCase(Chapter.TrainTracks)]
        public void WatchWarnsBeforeSprintingAndCatchingEvenWhenChapterCannotSprint(Chapter chapter)
        {
            var profile = WitchProfiles.For(chapter);
            Assert.IsFalse(profile.CanSprint);
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, false);
            var p = new WitchPerception
            {
                PlayerPosition = Vector3.forward * 0.5f, DistanceToPlayer = 0.5f, InWatchPassage = true
            };
            var decision = brain.Tick(0.1f, in p);
            Assert.AreEqual(WitchState.WindUp, decision.State);
            Assert.AreEqual(0f, decision.Speed);
            Assert.AreEqual(WitchState.WindUp, brain.Tick(profile.WindUpDuration - 0.01f, in p).State);
            decision = brain.Tick(0.02f, in p);
            Assert.AreEqual(WitchState.Sprinting, decision.State);
            Assert.AreEqual(profile.SprintSpeed, decision.Speed);
            Assert.AreEqual(WitchState.Caught, brain.Tick(0.1f, in p).State);
        }

        [TestCase(-1.5f, false)]
        [TestCase(-1.49f, true)]
        [TestCase(1.49f, true)]
        [TestCase(1.5f, false)]
        public void WatchSprintRespectsCatchHeightAfterWarning(float height, bool caught)
        {
            var profile = new WitchProfile { CanSprint = false };
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, false);
            var p = new WitchPerception
            {
                PlayerPosition = new Vector3(0f, height, 0.5f), DistanceToPlayer = 0.5f, InWatchPassage = true
            };
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
            Assert.AreEqual(WitchState.Sprinting, brain.Tick(profile.WindUpDuration, in p).State);
            Assert.AreEqual(caught ? WitchState.Caught : WitchState.Sprinting, brain.Tick(0.1f, in p).State);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void LosingTrackDuringWatchWarningCancelsSprint(bool vanished, bool safe, bool hidden)
        {
            var profile = new WitchProfile { CanSprint = false };
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, true);
            var p = new WitchPerception
            {
                PlayerPosition = Vector3.forward * 0.5f, DistanceToPlayer = 0.5f, InWatchPassage = true
            };
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
            p.PlayerVanished = vanished;
            p.PlayerInSafeZone = safe;
            p.PlayerHidden = hidden;
            Assert.AreEqual(WitchState.Stalking, brain.Tick(profile.WindUpDuration, in p).State);
            var decision = brain.Tick(profile.SprintDuration, in p);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(brain.Watch.Target, decision.MoveTarget);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WatchWarnsAgainAfterRecoveringInsteadOfCatchingByContact(bool canSprint)
        {
            var profile = new WitchProfile { CanSprint = canSprint };
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, false);
            var p = new WitchPerception
            {
                PlayerPosition = Vector3.forward * 0.5f, DistanceToPlayer = 0.5f, InWatchPassage = true
            };
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
            Assert.AreEqual(WitchState.Sprinting, brain.Tick(profile.WindUpDuration, in p).State);
            p.WitchPosition = Vector3.forward * (profile.CatchDistance + 1f);
            p.DistanceToPlayer = Vector3.Distance(p.WitchPosition, p.PlayerPosition);
            Assert.AreEqual(WitchState.Recovering, brain.Tick(profile.SprintDuration, in p).State);
            p.PlayerPosition = p.WitchPosition + Vector3.forward * 0.5f;
            p.DistanceToPlayer = 0.5f;
            Assert.AreEqual(WitchState.Stalking, brain.Tick(profile.RecoverDuration, in p).State);
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
        }

        [Test]
        public void WatchReturnsToPostAfterMissedSprintWhenPlayerLeavesPassage()
        {
            var profile = new WitchProfile { CanSprint = false };
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(Vector3.zero, false);
            var p = new WitchPerception { PlayerPosition = Vector3.forward * 4f, DistanceToPlayer = 4f, InWatchPassage = true };
            Assert.AreEqual(WitchState.WindUp, brain.Tick(0.1f, in p).State);
            Assert.AreEqual(WitchState.Sprinting, brain.Tick(profile.WindUpDuration, in p).State);
            p.InWatchPassage = false;
            p.WitchPosition = Vector3.forward * 8f;
            Assert.AreEqual(WitchState.Recovering, brain.Tick(profile.SprintDuration, in p).State);
            Assert.AreEqual(WitchState.Stalking, brain.Tick(profile.RecoverDuration, in p).State);
            var decision = brain.Tick(0.1f, in p);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(brain.Watch.Post, decision.MoveTarget);
            Assert.AreEqual(profile.FollowSpeed, decision.Speed);
        }

        [Test]
        public void WatchHoldsThePostWhereTheSameBrainWithoutWatchWouldLureSearchOrCircleTheCandle()
        {
            var profile = EagerProfile();
            var free = new WitchBrain(profile, null);
            var p = new WitchPerception { Raining = true, PlayerPosition = Vector3.right * 15f, PlayerForward = Vector3.forward };
            free.Tick(0f, in p);
            free.Tick(profile.LandingDuration, in p);
            p.Raining = false;
            Assert.AreEqual(WitchIntent.BarkLure, free.Tick(0f, in p).Intent, "Sin vigilancia el perfil ya ladra desde el bosque.");

            var post = Vector3.back * 2f;
            var brain = new WitchBrain(profile, null);
            brain.BeginWatch(post, false);
            p = new WitchPerception { WitchPosition = post, PlayerPosition = Vector3.right * 15f, PlayerForward = Vector3.forward,
                DistanceToPlayer = 15f, HasNextClearing = true, NextClearingPosition = Vector3.right * 80f };
            AssertHoldsPost(brain, post, 60f, in p);
            p.SeesPlayer = p.SmellsPlayer = true;
            AssertHoldsPost(brain, post, 0.1f, in p);
            p.SeesPlayer = p.SmellsPlayer = false;
            AssertHoldsPost(brain, post, 30f, in p);
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandlePosition = p.PlayerPosition;
            p.FamilyCandleRadius = 1.2f;
            AssertHoldsPost(brain, post, 30f, in p);
            p.HeardNoise = true;
            p.NoisePosition = p.PlayerPosition;
            AssertHoldsPost(brain, post, 0.1f, in p);
            Assert.IsFalse(brain.Watch.Investigating, "El ruido de Tiffany no es el señuelo.");
        }

        [Test]
        public void BeginWatchCancelsABarkLureAlreadyUnderway()
        {
            var profile = EagerProfile();
            var brain = new WitchBrain(profile, null);
            var p = new WitchPerception { Raining = true, PlayerPosition = Vector3.right * 15f, PlayerForward = Vector3.forward };
            brain.Tick(0f, in p);
            brain.Tick(profile.LandingDuration, in p);
            p.Raining = false;
            Assert.AreEqual(WitchIntent.BarkLure, brain.Tick(0f, in p).Intent);
            brain.BeginWatch(Vector3.zero, true);
            p.WitchPosition = Vector3.zero;
            AssertHoldsPost(brain, Vector3.zero, 0f, in p);
        }

        static WitchProfile EagerProfile()
        {
            var profile = WitchProfiles.For(Chapter.WitchHut);
            profile.BarkLureMinDelay = profile.BarkLureMaxDelay = 0f;
            profile.AmbushMinDelay = profile.AmbushMaxDelay = 0f;
            profile.RoamMinDelay = profile.RoamMaxDelay = 0f;
            profile.LoseTrackTime = 100f;
            return profile;
        }

        static void AssertHoldsPost(WitchBrain brain, Vector3 post, float dt, in WitchPerception p)
        {
            var decision = brain.Tick(dt, in p);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(WitchIntent.None, decision.Intent);
            Assert.AreEqual(post, decision.MoveTarget);
            Assert.AreEqual(0f, decision.Speed);
            Assert.IsFalse(decision.VoiceLure);
        }
    }
}
