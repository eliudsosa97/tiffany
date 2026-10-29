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
            Assert.AreEqual(8f, profile.ReturnGraceDuration);
            Assert.AreEqual(20f, profile.CatchUpDistance);
            Assert.AreEqual(6f, profile.CatchUpDelay);
            Assert.AreEqual(14f, profile.CatchUpReappearDistance);
            Assert.AreEqual(18f, profile.CatchUpReappearMaxDistance);
            Assert.Less(profile.CatchUpReappearMaxDistance, profile.CatchUpDistance);
            Assert.Greater(profile.CatchUpReappearDistance, profile.SprintTriggerDistance);
        }

        [TestCase(8f, 5f)]
        [TestCase(8f, 0.5f)]
        [TestCase(2f, 5f)]
        public void ReturnGraceIgnoresSensesUntilItsDurationThenWarnsBeforeLunging(float duration, float distance)
        {
            profile.ReturnGraceDuration = duration;
            p.PlayerPosition = p.NoisePosition = p.PlayerLightPosition = Vector3.forward * distance;
            p.SeesPlayer = p.SeesPlayerLight = p.SmellsPlayer = p.HeardNoise = true;
            brain.StartGrace();
            for (int i = 0; i < duration * 4f - 1f; i++)
            {
                WitchDecision decision = Step(0.25f);
                Assert.AreEqual(WitchState.Stalking, decision.State);
                Assert.AreEqual(WitchForm.Grounded, decision.Form);
                Assert.AreEqual(WitchIntent.None, decision.Intent);
                Assert.AreEqual(p.WitchPosition, decision.MoveTarget);
                Assert.AreEqual(0f, decision.Speed);
                Assert.IsFalse(brain.Chasing || decision.VoiceLure || decision.CatchUp);
            }
            Assert.AreEqual(WitchState.WindUp, Step(0.25f).State);
            Assert.IsTrue(brain.Chasing);
            Assert.AreEqual(WitchState.WindUp, Step(profile.WindUpDuration - 0.125f).State);
            Assert.AreEqual(WitchState.Sprinting, Step(0.125f).State);
            if (distance < profile.CatchDistance) Assert.AreEqual(WitchState.Caught, Step(0f).State);
        }

        [TestCase(0f)]
        [TestCase(2f)]
        public void ReappearanceClockExcludesGraceEvenWhenAFrameCrossesItsEnd(float afterGrace)
        {
            brain.StartGrace();
            Assert.IsFalse(Step(7.75f).CatchUp);
            Assert.IsFalse(Step(0.25f + afterGrace).CatchUp);
            Assert.IsFalse(Step(profile.CatchUpDelay - afterGrace - 0.25f).CatchUp);
            Assert.IsTrue(Step(0.25f).CatchUp);
        }

        [Test]
        public void GraceDoesNotRememberTheGirlForALaterSearch()
        {
            brain.StartGrace();
            p.PlayerPosition = p.NoisePosition = Vector3.forward * 5f;
            p.SeesPlayer = p.HeardNoise = true;
            Step(7.75f);
            p.SeesPlayer = p.HeardNoise = false;
            p.PlayerHidden = true;
            p.PlayerPosition = Vector3.forward * 25f;
            WitchDecision decision = Step(0.25f);
            Assert.AreEqual(WitchState.Stalking, decision.State);
            Assert.AreEqual(WitchIntent.None, decision.Intent);
            Assert.AreEqual(p.WitchPosition, decision.MoveTarget);
            Assert.AreEqual(0f, decision.Speed);
            Assert.IsFalse(brain.Chasing || decision.CatchUp);
        }

        [Test]
        public void FirstReturnHuntsAndWarnsWithoutGrace()
        {
            Assert.AreEqual(profile.FollowSpeed, Step(0f).Speed);
            p.PlayerPosition = Vector3.forward * 5f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            Assert.IsTrue(brain.Chasing);
        }

        [TestCase(Chapter.Cemetery, WitchState.Flying)]
        [TestCase(Chapter.WaterPlant, WitchState.Landing)]
        [TestCase(Chapter.TrainTracks, WitchState.Landing)]
        [TestCase(Chapter.Stable, WitchState.Landing)]
        [TestCase(Chapter.WitchHut, WitchState.Landing)]
        public void GraceDoesNotDelayDetectionOutsideReturn(Chapter chapter, WitchState expected)
        {
            profile = WitchProfiles.For(chapter);
            brain = new WitchBrain(profile, null);
            p.PlayerPosition = Vector3.forward * 5f;
            p.SeesPlayer = true;
            brain.StartGrace();
            Assert.AreEqual(expected, Step(0.25f).State);
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

        [Test]
        public void WhileHuntingAfterReappearingAHeardStepDoesNotTurnIntoASearchAndTheWarningFires()
        {
            Assert.IsTrue(Step(6f).CatchUp);
            brain.CatchUpPlaced(p.PlayerPosition);
            p.WitchPosition = p.PlayerPosition + Vector3.forward * profile.CatchUpReappearDistance;
            p.HeardNoise = true;
            p.NoisePosition = p.PlayerPosition;
            Step(0.1f);
            p.HeardNoise = false;
            for (int i = 0; i < 5; i++)
            {
                WitchDecision hunt = Step(0.1f);
                Assert.AreEqual(WitchIntent.None, hunt.Intent, "Tras reaparecer caza en vez de buscar.");
                Assert.AreEqual(WitchState.Stalking, hunt.State);
                Assert.AreEqual(p.PlayerPosition, hunt.MoveTarget);
                Assert.AreEqual(profile.FollowSpeed, hunt.Speed);
            }
            p.PlayerPosition = p.WitchPosition + Vector3.back * 8f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            Assert.AreEqual(WitchState.Sprinting, Step(profile.WindUpDuration).State);
        }

        [TestCase(WitchState.WindUp, "")]
        [TestCase(WitchState.Stunned, "")]
        [TestCase(WitchState.Caught, "")]
        [TestCase(WitchState.Stalking, "safe")]
        [TestCase(WitchState.Stalking, "hidden")]
        [TestCase(WitchState.Stalking, "vanished")]
        public void HuntingLastsUntilTheWarningOrUntilTheGirlIsProtected(WitchState state, string protection)
        {
            var catchUp = new WitchCatchUp();
            Assert.IsFalse(catchUp.Hunting);
            catchUp.Placed();
            catchUp.Tick(0.1f, profile, in p, WitchState.Stalking);
            Assert.IsTrue(catchUp.Hunting);
            p.PlayerInSafeZone = protection == "safe";
            p.PlayerHidden = protection == "hidden";
            p.PlayerVanished = protection == "vanished";
            catchUp.Tick(0.1f, profile, in p, state);
            Assert.IsFalse(catchUp.Hunting);
            p.PlayerInSafeZone = p.PlayerHidden = p.PlayerVanished = false;
            catchUp.Tick(0.1f, profile, in p, WitchState.Stalking);
            Assert.IsFalse(catchUp.Hunting);
        }

        [TestCase(14f, 0f, true)]
        [TestCase(16.9f, 0f, true)]
        [TestCase(17.1f, 0f, false)]
        [TestCase(22f, 0f, false)]
        [TestCase(10f, 0f, false)]
        [TestCase(14f, 19f, true)]
        [TestCase(14f, 21f, false)]
        [TestCase(14f, 180f, false)]
        public void PlacementMustStayBeyondTheWarningNearTheRequestedDistanceAndDirection(float distance, float angle, bool accepted)
        {
            Vector3 player = new Vector3(3f, 1f, -2f);
            Vector3 wanted = player + Vector3.forward * profile.CatchUpReappearDistance;
            Vector3 found = player + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance + Vector3.up * 0.5f;
            Assert.AreEqual(accepted, WitchCatchUp.AcceptsPlacement(player, wanted, found, profile));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(4f, 4f, 0f)]
        [TestCase(14f, 10f, 4f)]
        [TestCase(40f, 10f, 10f)]
        public void PointAlongPathWalksTheCornersByDistance(float distance, float x, float z)
        {
            var corners = new[] { Vector3.zero, Vector3.right * 10f, new Vector3(10f, 0f, 10f), Vector3.one * 99f };
            Vector3 point = WitchCatchUp.PointAlongPath(corners, 3, distance);
            Assert.Less(Vector3.Distance(new Vector3(x, 0f, z), point), 1e-4f);
        }

        private WitchDecision Step(float dt)
        {
            Vector3 offset = p.PlayerPosition - p.WitchPosition;
            offset.y = 0f;
            p.DistanceToPlayer = offset.magnitude;
            return brain.Tick(dt, in p);
        }

        [Test]
        public void SideReappearanceBeatsAFrontOneOnlyWhenThePathsAreClose()
        {
            Assert.Less(WitchCatchUp.PlacementCost(16f, 40f), WitchCatchUp.PlacementCost(14f, 0f), "De lado gana a un frente apenas más corto.");
            Assert.Less(WitchCatchUp.PlacementCost(14f, 0f), WitchCatchUp.PlacementCost(20f, 60f), "El frente gana si el lado queda mucho más lejos.");
        }
    }
}
