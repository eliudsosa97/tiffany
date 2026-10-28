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
            var brain = new WitchBrain(new WitchProfile(), null);
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
            Assert.AreEqual(WitchState.Caught, brain.Tick(0.1f, in p).State);
        }
    }
}
