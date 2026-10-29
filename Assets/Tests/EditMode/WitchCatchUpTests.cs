using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchCatchUpTests
    {
        private WitchProfile profile;
        private WitchBrain brain;
        private WitchPerception p;

        [SetUp]
        public void SetUp()
        {
            profile = WitchProfiles.For(Chapter.Return);
            brain = new WitchBrain(profile, null);
            p = new WitchPerception { PlayerPosition = Vector3.forward * 25f, PlayerForward = Vector3.forward };
        }

        [Test]
        public void ReturnProfileUsesTwentyMetersSixSecondsAndFourteenMetersAhead()
        {
            Assert.AreEqual(20f, profile.CatchUpDistance);
            Assert.AreEqual(6f, profile.CatchUpDelay);
            Assert.AreEqual(14f, profile.CatchUpReappearDistance);
            Assert.Greater(profile.CatchUpReappearDistance, profile.SprintTriggerDistance);
        }

        [Test]
        public void RequestsReappearanceAfterSixSecondsFarAndRetriesOneSecondAfterABlockedPlacement()
        {
            Assert.IsFalse(Step(3f).CatchUp);
            WitchDecision decision = Step(3f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.IsTrue(decision.CatchUp);
            brain.CatchUpBlocked();
            Assert.IsFalse(Step(0.99f).CatchUp);
            Assert.IsTrue(Step(0.02f).CatchUp);
        }

        [Test]
        public void DoesNotRequestAtFivePointNineSeconds()
        {
            WitchDecision decision = Step(5.9f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.IsFalse(decision.CatchUp);
        }

        [Test]
        public void ClockRestartsWhenTheWitchGetsWithinTwentyMeters()
        {
            Assert.IsFalse(Step(5f).CatchUp);
            p.PlayerPosition = Vector3.forward * 15f;
            Assert.IsFalse(Step(0.5f).CatchUp);
            p.PlayerPosition = Vector3.forward * 25f;
            Assert.IsFalse(Step(1.5f).CatchUp);
            Assert.IsTrue(Step(4.5f).CatchUp);
        }

        [TestCase(WitchState.WindUp)]
        [TestCase(WitchState.Sprinting)]
        [TestCase(WitchState.Recovering)]
        [TestCase(WitchState.Stunned)]
        [TestCase(WitchState.Caught)]
        public void ClockDoesNotRunWhileWarningLungingRecoveringStunnedOrCaught(WitchState state)
        {
            if (state == WitchState.WindUp) profile.WindUpDuration = 100f;
            if (state == WitchState.Sprinting) profile.SprintDuration = 100f;
            if (state == WitchState.Recovering) profile.RecoverDuration = 100f;
            profile.StunDuration = 100f;
            if (state == WitchState.Stunned)
            {
                p.Flashed = true;
                Assert.AreEqual(WitchState.Stunned, Step(0f).State);
                p.Flashed = false;
            }
            else
            {
                p.PlayerPosition = Vector3.forward * 8f;
                Assert.AreEqual(WitchState.WindUp, Step(0f).State);
                if (state != WitchState.WindUp) Assert.AreEqual(WitchState.Sprinting, Step(profile.WindUpDuration).State);
                if (state == WitchState.Recovering) Assert.AreEqual(WitchState.Recovering, Step(profile.SprintDuration).State);
                if (state == WitchState.Caught)
                {
                    p.PlayerPosition = Vector3.forward * 0.5f;
                    Assert.AreEqual(WitchState.Caught, Step(0f).State);
                }
            }
            p.PlayerPosition = Vector3.forward * 25f;
            WitchDecision decision = Step(6f);
            Assert.AreEqual(state, decision.State);
            Assert.IsFalse(decision.CatchUp);
        }

        [Test]
        public void OnlyAppliesToTheGroundChase()
        {
            profile = new WitchProfile();
            brain = new WitchBrain(profile, null);
            p.Raining = true;
            Step(0f);
            Assert.AreEqual(WitchState.Stalking, Step(profile.LandingDuration).State);
            WitchDecision decision = Step(6f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.IsFalse(decision.CatchUp);
        }

        [TestCase("safe")]
        [TestCase("vanished")]
        [TestCase("hidden")]
        public void DoesNotApplyWhileTheGirlIsSafeVanishedOrHidden(string protection)
        {
            p.PlayerInSafeZone = protection == "safe";
            p.PlayerVanished = protection == "vanished";
            p.PlayerHidden = protection == "hidden";
            WitchDecision decision = Step(6f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.IsFalse(decision.CatchUp);
        }

        [Test]
        public void AfterReappearingHuntsTheGirlAndAlwaysWarnsBeforeTheLunge()
        {
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            Vector3 lastSeen = p.PlayerPosition;
            p.PlayerPosition = Vector3.forward * 60f;
            WitchDecision far = Step(6f);
            Assert.AreEqual(WitchIntent.LastSeen, far.Intent);
            Assert.IsTrue(far.CatchUp);

            brain.CatchUpPlaced(p.PlayerPosition);
            p.WitchPosition = p.PlayerPosition + Vector3.forward * profile.CatchUpReappearDistance;
            WitchDecision hunt = Step(0f);
            Assert.AreEqual(WitchState.Stalking, hunt.State);
            Assert.AreEqual(WitchIntent.None, hunt.Intent);
            Assert.AreEqual(p.PlayerPosition, hunt.MoveTarget);
            Assert.AreNotEqual(lastSeen, hunt.MoveTarget);
            Assert.AreEqual(profile.FollowSpeed, hunt.Speed);
            Assert.IsFalse(hunt.CatchUp);

            p.PlayerPosition = p.WitchPosition + Vector3.back * 8f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            p.PlayerPosition = p.WitchPosition + Vector3.back * 0.5f;
            Assert.AreEqual(WitchState.WindUp, Step(profile.WindUpDuration - 0.01f).State, "Sin aviso completo no embiste ni atrapa.");
            Assert.AreEqual(WitchState.Sprinting, Step(0.02f).State);
        }

        private WitchDecision Step(float dt)
        {
            Vector3 offset = p.PlayerPosition - p.WitchPosition;
            offset.y = 0f;
            p.DistanceToPlayer = offset.magnitude;
            return brain.Tick(dt, in p);
        }
    }
}
