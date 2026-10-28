using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class ReturnChaseTests
    {
        WitchBrain brain;
        WitchPerception perception;

        [SetUp]
        public void SetUp()
        {
            brain = new WitchBrain(WitchProfiles.For(Chapter.Return), new[] { Vector3.up * 14f });
            perception = new WitchPerception { PlayerPosition = Vector3.forward * 20f };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FollowsOnFootWithoutPerceptionOrFlightInEitherWeather(bool raining)
        {
            perception.Raining = raining;
            var decision = Step(9f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(WitchForm.Grounded, decision.Form);
            Assert.AreEqual(2.5f, decision.Speed);
            Assert.AreEqual(perception.PlayerPosition, decision.MoveTarget);
            Assert.IsFalse(decision.FireLit);
            Assert.IsFalse(decision.VoiceLure);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void ProtectionBlocksCaptureAndNeverRestoresFlight(bool safe, bool vanished, bool hidden)
        {
            Step(0f);
            perception.PlayerInSafeZone = safe;
            perception.PlayerVanished = vanished;
            perception.PlayerHidden = hidden;
            perception.PlayerPosition = Vector3.forward * 0.5f;
            var decision = Step(20f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(WitchForm.Grounded, decision.Form);
            perception.PlayerInSafeZone = perception.PlayerVanished = perception.PlayerHidden = false;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
        }

        [Test]
        public void ReturnReusesWarningStraightSprintRecoveryAndFollowSpeeds()
        {
            perception.PlayerPosition = Vector3.forward * 8f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            var warning = Step(0.59f);
            Assert.AreEqual(WitchState.WindUp, warning.State);
            Assert.AreEqual(0f, warning.Speed);
            var sprint = Step(0.02f);
            Assert.AreEqual(WitchState.Sprinting, sprint.State);
            Assert.AreEqual(9f, sprint.Speed);
            Assert.That(Vector3.Distance(Vector3.forward * 10.8f, sprint.MoveTarget), Is.LessThan(0.001f));
            perception.PlayerPosition = Vector3.right * 20f;
            var straight = Step(1.19f);
            Assert.AreEqual(WitchState.Sprinting, straight.State);
            Assert.AreEqual(sprint.MoveTarget, straight.MoveTarget);
            Assert.AreEqual(9f, straight.Speed);
            Assert.AreEqual(WitchState.Recovering, Step(0.02f).State);
            Assert.AreEqual(WitchState.Recovering, Step(0.79f).State);
            var follow = Step(0.02f);
            Assert.AreEqual(WitchState.Stalking, follow.State);
            Assert.AreEqual(2.5f, follow.Speed);
            Assert.AreEqual(perception.PlayerPosition, follow.MoveTarget);
        }

        [Test]
        public void FlashDuringWarningInterruptsLungeThenReturnContinuesOnFoot()
        {
            perception.PlayerPosition = Vector3.forward * 8f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            perception.Flashed = true;
            Assert.AreEqual(WitchState.Stunned, Step(0.3f).State);
            perception.Flashed = false;
            var stunned = Step(1.99f);
            Assert.AreEqual(WitchState.Stunned, stunned.State);
            Assert.AreEqual(0f, stunned.Speed);
            var follow = Step(0.02f);
            Assert.AreEqual(WitchState.Stalking, follow.State);
            Assert.AreEqual(WitchForm.Grounded, follow.Form);
            Assert.AreEqual(2.5f, follow.Speed);
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
        }

        WitchDecision Step(float dt)
        {
            perception.DistanceToPlayer = Vector3.Distance(perception.WitchPosition, perception.PlayerPosition);
            return brain.Tick(dt, in perception);
        }
    }
}
