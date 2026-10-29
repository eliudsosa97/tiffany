using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchGameTests
    {
        WitchProfile profile;
        WitchBrain brain;
        WitchPerception perception;

        [SetUp]
        public void SetUp()
        {
            profile = new WitchProfile();
            brain = new WitchBrain(profile, null, new FixedRandom(0.5));
            perception = new WitchPerception { PlayerPosition = Vector3.right * 15f };
        }

        [TestCase(0.0, 45f, 8f)]
        [TestCase(0.5, 52.5f, 10f)]
        [TestCase(1.0, 60f, 12f)]
        public void RoamingUsesInjectedDelayRadiusAndRouteHeightThenResumesRoute(double random, float delay, float radius)
        {
            var route = new[] { new Vector3(-40f, 14f, 0f), new Vector3(-40f, 14f, 20f) };
            profile.FlightHeightOffset = 10f;
            brain = new WitchBrain(profile, route, new FixedRandom(random));
            perception.PlayerPosition = Vector3.zero;
            perception.WitchPosition = new Vector3(-60f, 24f, -20f);
            Assert.AreEqual(route[0] + Vector3.up * 10f, Step(delay - 0.25f).MoveTarget);

            WitchDecision pass = Step(0.25f);

            Assert.AreEqual(WitchState.Flying, pass.State);
            Assert.AreEqual(24f, pass.MoveTarget.y);
            Assert.AreEqual(radius, HorizontalDistance(pass.MoveTarget, perception.PlayerPosition), 1e-4f);
            perception.WitchPosition = pass.MoveTarget;
            Assert.AreEqual(route[0] + Vector3.up * 10f, Step(0f).MoveTarget);
            perception.WitchPosition = new Vector3(-60f, 24f, -20f);
            Assert.AreEqual(route[0] + Vector3.up * 10f, Step(delay - 0.25f).MoveTarget);
            Assert.AreEqual(radius, HorizontalDistance(Step(0.25f).MoveTarget, perception.PlayerPosition), 1e-4f);
        }

        [Test]
        public void PassingNearResetsFarTimerUsingConfiguredHorizontalDistance()
        {
            profile.FarDistance = 30f;
            perception.PlayerPosition = Vector3.zero;
            perception.WitchPosition = new Vector3(-40f, 200f, 0f);
            Step(50f);
            perception.WitchPosition.x = -30f;
            Step(1f);
            perception.WitchPosition.x = -40f;
            perception.PlayerPosition = Vector3.zero;
            Assert.AreEqual(perception.WitchPosition, Step(50f).MoveTarget);
            Assert.AreNotEqual(perception.WitchPosition, Step(2.5f).MoveTarget);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotAdvanceRoamingOrSuspicion(float dt)
        {
            perception.WitchPosition = Vector3.left * 40f;
            Assert.AreEqual(perception.WitchPosition, Step(dt).MoveTarget);
            Ground();
            HearPlayer();
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            perception.HeardNoise = false;
            Assert.AreEqual(WitchState.Suspicious, Step(dt).State);
        }

        [Test]
        public void DistantNoiseStopsAndFacesItsOriginBeforeReturningToStalking()
        {
            Ground();
            HearPlayer();
            WitchDecision suspicion = Step(0f);
            Assert.AreEqual(WitchState.Suspicious, suspicion.State);
            Assert.AreEqual(WitchForm.Grounded, suspicion.Form);
            Assert.AreEqual(perception.NoisePosition, suspicion.MoveTarget);
            Assert.AreEqual(0f, suspicion.Speed);
            Assert.IsFalse(suspicion.VoiceLure);
            perception.HeardNoise = false;
            Assert.AreEqual(WitchState.Suspicious, Step(profile.SuspicionDuration - 0.1f).State);
            WitchDecision resumed = Step(0.11f);
            Assert.AreEqual(WitchState.Stalking, resumed.State);
            Assert.AreEqual(profile.StalkSpeed, resumed.Speed);
            Assert.IsTrue(resumed.VoiceLure);
        }

        [Test]
        public void SecondNoiseWithinSuspicionStartsFollowingThePlayer()
        {
            Ground();
            HearPlayer();
            Step(0f);
            perception.HeardNoise = false;
            Step(1f);
            HearPlayer();
            WitchDecision chase = Step(0f);
            Assert.AreEqual(WitchState.Stalking, chase.State);
            Assert.AreEqual(profile.FollowSpeed, chase.Speed);
            Assert.AreEqual(perception.PlayerPosition, chase.MoveTarget);
            Assert.IsFalse(chase.VoiceLure);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FifteenMeterEncounterUsesRealSensesForOneOrTwoNoises(bool secondNoise)
        {
            Ground();
            var senses = new WitchSenses(new WitchSenseProfile(), new[] { new Vector3(4f, 0f, 16f) });
            var input = new WitchSenseInputs
            {
                WitchPosition = Vector3.right * 15f,
                Form = WitchForm.Grounded,
                LineOfSight = true
            };
            senses.Hear(new StepNoise(Vector3.zero, 20f), input.WitchPosition);
            WitchDecision decision = brain.Tick(0f, senses.Sense(in input));
            Assert.AreEqual(WitchState.Suspicious, decision.State);
            bool warned = false;
            bool inspected = false;
            var cadence = new StepCadence();
            const float dt = 1f / 30f;
            for (int frame = 1; frame <= WitchDemo.NothingDuration / dt; frame++)
            {
                if (secondNoise && frame == 30) senses.Hear(new StepNoise(Vector3.zero, 20f), input.WitchPosition);
                if (!secondNoise)
                {
                    Vector3 previous = input.PlayerPosition;
                    input.PlayerPosition = Vector3.MoveTowards(previous, Vector3.left * 8f, PlayerMotion.SlowSpeed * dt);
                    if (cadence.Advance(Vector3.Distance(previous, input.PlayerPosition), Gait.Slow))
                        senses.Hear(new StepNoise(input.PlayerPosition, NoiseModel.StepRadius(GroundType.Dirt, Gait.Slow, false)),
                            input.WitchPosition);
                }
                decision = brain.Tick(dt, senses.Sense(in input));
                warned |= decision.State == WitchState.WindUp;
                inspected |= decision.State == WitchState.Stalking && decision.Speed == 0f
                    && HorizontalDistance(input.WitchPosition, Vector3.zero) <= profile.ArriveDistance;
                if (decision.State == WitchState.Sprinting)
                {
                    Assert.IsTrue(secondNoise && warned);
                    return;
                }
                if (decision.State == WitchState.TakingOff)
                {
                    Assert.IsFalse(secondNoise || warned);
                    Assert.IsTrue(inspected);
                    return;
                }
                input.Form = decision.Form;
                input.WitchPosition = Vector3.MoveTowards(input.WitchPosition, decision.MoveTarget, decision.Speed * dt);
            }
            Assert.Fail("El encuentro debe terminar en embestida con dos ruidos o en revisión y despegue con uno.");
        }

        [Test]
        public void MutedNoiseDoesNotConfirmSuspicion()
        {
            Ground();
            HearPlayer();
            Step(0f);
            perception.HearingMuted = true;
            Assert.AreEqual(WitchState.Suspicious, Step(1f).State);
            Assert.AreEqual(WitchState.Stalking, Step(profile.SuspicionDuration).State);
            Assert.AreEqual(profile.StalkSpeed, Step(0f).Speed);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ClearPerceptionDuringSuspicionWarnsBeforeCharging(bool sight)
        {
            Ground();
            HearPlayer();
            Step(0f);
            perception.HeardNoise = !sight;
            perception.SeesPlayer = sight;
            perception.PlayerPosition = Vector3.right * 0.5f;
            perception.NoisePosition = perception.PlayerPosition;
            Assert.AreEqual(WitchState.WindUp, Step(0.5f).State);
        }

        [Test]
        public void NoiseAfterSuspicionExpiredStartsANewSuspicion()
        {
            Ground();
            HearPlayer();
            Step(0f);
            perception.HeardNoise = false;
            Assert.AreEqual(WitchState.Stalking, Step(profile.SuspicionDuration).State);
            HearPlayer();
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
        }

        [TestCase(5f, WitchState.WindUp)]
        [TestCase(4.9f, WitchState.Suspicious)]
        public void ClearNoiseDistanceIsConfigurable(float clearDistance, WitchState expected)
        {
            Ground();
            profile.ClearNoiseDistance = clearDistance;
            perception.PlayerPosition = Vector3.right * 5f;
            HearPlayer();
            Assert.AreEqual(expected, Step(0f).State);
        }

        [Test]
        public void ContinuousDistantLightIsOneGlimpseAndSuspicionResumesFlight()
        {
            perception.PlayerLightPosition = perception.PlayerPosition = Vector3.right * 40f;
            perception.SeesPlayerLight = true;
            WitchDecision suspicion = Step(0f);
            Assert.AreEqual(WitchState.Suspicious, suspicion.State);
            Assert.AreEqual(WitchForm.Flying, suspicion.Form);
            Assert.IsTrue(suspicion.FireLit);
            Assert.AreEqual(0f, suspicion.Speed);
            for (int i = 0; i < 20; i++) Assert.AreEqual(WitchState.Suspicious, Step(0.1f).State);
            Assert.AreEqual(WitchState.Flying, Step(1.01f).State);
            Assert.AreEqual(perception.WitchPosition, Step(0f).MoveTarget);
        }

        [Test]
        public void SecondLightGlimpseConfirmsAnAirborneTarget()
        {
            perception.PlayerLightPosition = perception.PlayerPosition = Vector3.right * 40f;
            perception.SeesPlayerLight = true;
            Step(0f);
            perception.SeesPlayerLight = false;
            Step(0.5f);
            perception.SeesPlayerLight = true;
            WitchDecision confirmed = Step(0f);
            Assert.AreEqual(WitchState.Flying, confirmed.State);
            Assert.AreEqual(perception.PlayerLightPosition.x, confirmed.MoveTarget.x);
        }

        [TestCase(20f, WitchState.Flying)]
        [TestCase(19.9f, WitchState.Suspicious)]
        public void ClearLightDistanceIsConfigurable(float clearDistance, WitchState expected)
        {
            profile.ClearLightDistance = clearDistance;
            perception.SeesPlayerLight = true;
            perception.PlayerLightPosition = Vector3.right * 20f;
            Assert.AreEqual(expected, Step(0f).State);
        }

        [Test]
        public void RainInterruptsAirborneSuspicionAndFlashInterruptsGroundSuspicion()
        {
            perception.SeesPlayerLight = true;
            perception.PlayerLightPosition = Vector3.right * 40f;
            Step(0f);
            perception.Raining = true;
            Assert.AreEqual(WitchState.Landing, Step(0f).State);
            Step(profile.LandingDuration);
            perception.SeesPlayerLight = perception.Raining = false;
            HearPlayer();
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            perception.Flashed = true;
            Assert.AreEqual(WitchState.Stunned, Step(0f).State);
        }

        [TestCase(WitchForm.Flying, 19.9f, TylerReaction.Growl)]
        [TestCase(WitchForm.Grounded, 19.9f, TylerReaction.Growl)]
        [TestCase(WitchForm.Flying, 20f, TylerReaction.Whine)]
        [TestCase(WitchForm.Grounded, 20f, TylerReaction.None)]
        [TestCase(WitchForm.Flying, float.NaN, TylerReaction.None)]
        public void TylerGrowlsAtSuspicionInsideHisRadius(WitchForm form, float distance, TylerReaction expected)
        {
            var inputs = new TylerInputs { WitchSuspicious = true, WitchForm = form, WitchDistance = distance };
            Assert.AreEqual(expected, new Tyler().Tick(0f, in inputs));
        }

        [Test]
        public void DefaultWarningLastsEightTenthsAndCanBeTuned()
        {
            Assert.AreEqual(0.8f, profile.WindUpDuration);
            Ground();
            perception.PlayerPosition = Vector3.right * 5f;
            perception.SeesPlayer = true;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            Assert.AreEqual(0f, Step(0.79f).Speed);
            Assert.AreEqual(WitchState.Sprinting, Step(0.02f).State);
            SetUp();
            profile.WindUpDuration = 1.5f;
            Ground();
            perception.PlayerPosition = Vector3.right * 5f;
            perception.SeesPlayer = true;
            Step(0f);
            Assert.AreEqual(WitchState.WindUp, Step(1.49f).State);
            Assert.AreEqual(WitchState.Sprinting, Step(0.02f).State);
        }

        [TestCase(Chapter.Cemetery)]
        [TestCase(Chapter.WaterPlant)]
        [TestCase(Chapter.TrainTracks)]
        [TestCase(Chapter.Stable)]
        [TestCase(Chapter.WitchHut)]
        [TestCase(Chapter.Return)]
        public void EverySprintIncludingAfterRecoveryAndStunPassesThroughAFullWarning(Chapter chapter)
        {
            profile = WitchProfiles.For(chapter);
            brain = new WitchBrain(profile, null, new FixedRandom(0.5));
            perception.PlayerPosition = Vector3.right * 8f;
            perception.Raining = true;
            perception.SeesPlayer = true;
            WitchState previous = Step(0f).State;
            float warning = 0f;
            int sprints = 0;
            for (int i = 0; i < 300; i++)
            {
                perception.Flashed = i == 55;
                if (previous == WitchState.WindUp) warning += 0.1f;
                WitchDecision decision = Step(0.1f);
                if (decision.State == WitchState.Sprinting && previous != WitchState.Sprinting)
                {
                    Assert.AreEqual(WitchState.WindUp, previous);
                    Assert.GreaterOrEqual(warning + 1e-5f, profile.WindUpDuration);
                    sprints++;
                }
                if (decision.State != WitchState.WindUp) warning = 0f;
                previous = decision.State;
            }
            if (profile.CanSprint) Assert.Greater(sprints, 2);
            else Assert.AreEqual(0, sprints);
        }

        [Test]
        public void SensesChooseHideNearestToNoiseInsteadOfWitchOrPlayerAndCopyTheCache()
        {
            var points = new[] { Vector3.right, Vector3.right * 14f, Vector3.right * 80f };
            var senses = new WitchSenses(new WitchSenseProfile(), points);
            points[1] = Vector3.right * 100f;
            var input = new WitchSenseInputs { Form = WitchForm.Grounded, PlayerPosition = Vector3.right * 80f };
            senses.Hear(new StepNoise(Vector3.right * 15f, 20f), Vector3.zero);
            WitchPerception result = senses.Sense(in input);
            Assert.IsTrue(result.HasNoiseHide);
            Assert.AreEqual(Vector3.right * 14f, result.NoiseHidePosition);
            Assert.IsFalse(senses.Sense(in input).HasNoiseHide);
            senses = new WitchSenses(new WitchSenseProfile());
            senses.Hear(new StepNoise(Vector3.right * 15f, 20f), Vector3.zero);
            Assert.IsFalse(senses.Sense(in input).HasNoiseHide);
        }

        [TestCase(5f, true)]
        [TestCase(4.9f, false)]
        public void OnlyHidesWithinConfiguredHorizontalRadiusAreInvestigated(float radius, bool expected)
        {
            var senses = new WitchSenses(new WitchSenseProfile { NoiseHideRadius = radius },
                new[] { new Vector3(20f, 30f, 0f), Vector3.right * 80f });
            var input = new WitchSenseInputs { Form = WitchForm.Grounded };
            senses.Hear(new StepNoise(Vector3.right * 15f, 20f), Vector3.zero);

            WitchPerception result = senses.Sense(in input);

            Assert.IsTrue(result.HeardNoise);
            Assert.AreEqual(expected, result.HasNoiseHide);
            Assert.AreEqual(expected ? new Vector3(20f, 30f, 0f) : Vector3.zero, result.NoiseHidePosition);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecoyWithoutNearbyHideIsReachedInspectedAndLeft(bool raining)
        {
            Ground();
            perception.Raining = raining;
            perception.PlayerHidden = true;
            var senses = new WitchSenses(new WitchSenseProfile(), new[] { Vector3.left * 20f });
            var input = new WitchSenseInputs { Form = WitchForm.Grounded };
            senses.Hear(new StepNoise(Vector3.right * 15f, 20f), Vector3.zero);
            WitchPerception heard = senses.Sense(in input);
            perception.HeardNoise = heard.HeardNoise;
            perception.NoisePosition = heard.NoisePosition;
            perception.HasNoiseHide = heard.HasNoiseHide;
            Assert.IsFalse(perception.HasNoiseHide);
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            perception.HeardNoise = false;
            WitchDecision search = Step(profile.SuspicionDuration);
            Assert.AreEqual(WitchState.Stalking, search.State);
            Assert.AreEqual(heard.NoisePosition, search.MoveTarget);
            for (int i = 1; i <= 4; i++)
            {
                perception.WitchPosition = Vector3.right * i * 3f;
                Assert.AreEqual(profile.StalkSpeed, Step(3f).Speed);
            }
            perception.WitchPosition = heard.NoisePosition;
            Assert.AreEqual(0f, Step(0f).Speed);
            WitchDecision review = Step(profile.HideInspectionDuration - 0.1f);
            Assert.AreEqual(WitchState.Stalking, review.State);
            Assert.AreEqual(0f, review.Speed);
            Assert.IsFalse(review.InspectingHide);
            WitchDecision leaving = Step(0.11f);
            Assert.AreEqual(raining ? WitchState.Stalking : WitchState.TakingOff, leaving.State);
            if (raining)
            {
                Assert.AreEqual(profile.StalkSpeed, leaving.Speed);
                Assert.AreEqual(profile.HideRetreatDistance, HorizontalDistance(perception.WitchPosition, leaving.MoveTarget), 1e-4f);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ClearPerceptionDuringNoiseInvestigationChasesThenWarns(bool hide, bool arrived)
        {
            Ground();
            HearPlayer();
            perception.HasNoiseHide = hide;
            perception.NoiseHidePosition = Vector3.right * 14f;
            Step(0f);
            perception.HeardNoise = false;
            WitchDecision search = Step(profile.SuspicionDuration);
            if (arrived)
            {
                perception.WitchPosition = search.MoveTarget;
                Assert.AreEqual(0f, Step(0f).Speed);
            }
            perception.PlayerPosition = perception.WitchPosition + Vector3.forward * 14f;
            perception.SeesPlayer = true;
            WitchDecision chase = Step(0f);
            Assert.AreEqual(WitchState.Stalking, chase.State);
            Assert.AreEqual(perception.PlayerPosition, chase.MoveTarget);
            Assert.AreEqual(profile.FollowSpeed, chase.Speed);
            Assert.IsFalse(chase.InspectingHide || chase.VoiceLure);
            perception.PlayerPosition = perception.WitchPosition + Vector3.forward * 5f;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            Assert.AreEqual(0f, Step(profile.WindUpDuration - 0.01f).Speed);
            Assert.AreEqual(WitchState.Sprinting, Step(0.02f).State);
        }

        [Test]
        public void NoiseInvestigationReachesHideWaitsThereThenLeavesWithoutFindingUnseenPlayer()
        {
            Ground();
            HearPlayer();
            perception.HasNoiseHide = true;
            perception.NoiseHidePosition = Vector3.right * 14f;
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            perception.HeardNoise = false;
            perception.PlayerHidden = true;
            WitchDecision search = Step(profile.SuspicionDuration);
            Assert.AreEqual(perception.NoiseHidePosition, search.MoveTarget);
            Assert.IsFalse(search.InspectingHide);
            for (int i = 1; i <= 3; i++)
            {
                perception.WitchPosition = Vector3.right * i * 3f;
                Assert.AreEqual(WitchState.Stalking, Step(3f).State);
            }
            perception.WitchPosition = perception.NoiseHidePosition;
            perception.SeesPlayer = perception.SmellsPlayer = true;
            WitchDecision review = Step(0f);
            Assert.IsTrue(review.InspectingHide);
            Assert.AreEqual(0f, review.Speed);
            Assert.AreEqual(WitchState.Stalking, Step(profile.HideInspectionDuration - 0.1f).State);
            Assert.AreEqual(WitchState.TakingOff, Step(0.11f).State);
        }

        [Test]
        public void RainMakesWitchLeaveReviewedHideOnFoot()
        {
            Ground();
            perception.Raining = true;
            perception.PlayerHidden = true;
            HearPlayer();
            perception.HasNoiseHide = true;
            perception.NoiseHidePosition = Vector3.right * 14f;
            Step(0f);
            perception.HeardNoise = false;
            Step(profile.SuspicionDuration);
            perception.WitchPosition = perception.NoiseHidePosition;
            Step(0f);
            WitchDecision leaving = Step(profile.HideInspectionDuration);
            Assert.AreEqual(WitchState.Stalking, leaving.State);
            Assert.IsFalse(leaving.InspectingHide);
            Assert.Greater(leaving.Speed, 0f);
            Assert.AreEqual(profile.HideRetreatDistance, HorizontalDistance(perception.WitchPosition, leaving.MoveTarget), 1e-4f);
            Assert.Less(leaving.MoveTarget.x, perception.WitchPosition.x);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RainRetreatIntoSafeZoneUsesCorrectedTargetAndFinishesEvenWhenBlocked(bool blocked)
        {
            brain = new WitchBrain(profile, null, resolveGroundTarget: point => SafeZones.PushOut(point, 1f));
            Ground();
            perception.Raining = perception.PlayerHidden = true;
            HearPlayer();
            perception.HasNoiseHide = true;
            perception.NoiseHidePosition = Vector3.right * 14f;
            Step(0f);
            perception.HeardNoise = false;
            Step(profile.SuspicionDuration);
            perception.WitchPosition = perception.NoiseHidePosition;
            Step(0f);
            Vector3 original = perception.WitchPosition + Vector3.left * profile.HideRetreatDistance;
            int candle = SafeZones.Add(original, 2f);
            try
            {
                WitchDecision leaving = Step(profile.HideInspectionDuration);
                Assert.IsTrue(SafeZones.Contains(original));
                Assert.IsFalse(SafeZones.Contains(leaving.MoveTarget));
                Assert.AreEqual(SafeZones.PushOut(original, 1f), leaving.MoveTarget);
                Assert.AreEqual(profile.StalkSpeed, leaving.Speed);
                if (blocked)
                {
                    Assert.AreEqual(profile.StalkSpeed, Step(profile.LoseTrackTime - 0.1f).Speed);
                }
                else perception.WitchPosition = leaving.MoveTarget;
                WitchDecision finished = Step(0.11f);
                Assert.AreEqual(WitchState.Stalking, finished.State);
                Assert.AreEqual(0f, finished.Speed);
                Assert.IsFalse(finished.InspectingHide);
                Assert.AreEqual(0f, Step(profile.LoseTrackTime * 2f).Speed);
            }
            finally
            {
                SafeZones.Remove(candle);
            }
        }

        [Test]
        public void UnreachableHideDoesNotKeepWitchSearchingForever()
        {
            Ground();
            HearPlayer();
            perception.HasNoiseHide = true;
            perception.NoiseHidePosition = Vector3.right * 14f;
            Step(0f);
            perception.HeardNoise = false;
            Step(profile.SuspicionDuration);
            Assert.AreEqual(WitchState.Stalking, Step(profile.LoseTrackTime - 0.1f).State);
            Assert.AreEqual(WitchState.TakingOff, Step(0.11f).State);
        }

        [Test]
        public void SeenEntryStillAllowsCaptureWhileUnseenEntryCancelsWarning()
        {
            Ground();
            perception.PlayerPosition = Vector3.right * 5f;
            perception.SeesPlayer = true;
            Step(0f);
            perception.PlayerHidden = true;
            perception.SeesPlayer = false;
            Assert.AreEqual(WitchState.Sprinting, Step(profile.WindUpDuration).State);
            perception.WitchPosition = perception.PlayerPosition;
            Assert.AreEqual(WitchState.Caught, Step(0f).State);
            SetUp();
            Ground();
            perception.PlayerPosition = Vector3.right * 5f;
            HearPlayer();
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            perception.HeardNoise = false;
            perception.PlayerHidden = true;
            Assert.AreEqual(WitchState.Stalking, Step(profile.WindUpDuration).State);
        }

        [Test]
        public void HiddenPlayerLightCannotStartAnAirborneHunt()
        {
            perception.PlayerHidden = perception.SeesPlayerLight = perception.SeesPlayer = true;
            perception.PlayerLightPosition = Vector3.right;
            Assert.AreEqual(WitchState.Flying, Step(0f).State);
            Assert.AreEqual(perception.WitchPosition, Step(0f).MoveTarget);
        }

        void Ground()
        {
            perception.WitchPosition = Vector3.zero;
            perception.Raining = true;
            Step(0f);
            Step(profile.LandingDuration);
            perception.Raining = false;
        }

        void HearPlayer()
        {
            perception.HeardNoise = true;
            perception.NoisePosition = perception.PlayerPosition;
        }

        WitchDecision Step(float dt)
        {
            perception.DistanceToPlayer = HorizontalDistance(perception.WitchPosition, perception.PlayerPosition);
            return brain.Tick(dt, in perception);
        }

        static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }

        sealed class FixedRandom : System.Random
        {
            readonly double value;
            public FixedRandom(double value) => this.value = value;
            public override double NextDouble() => value;
        }
    }
}
