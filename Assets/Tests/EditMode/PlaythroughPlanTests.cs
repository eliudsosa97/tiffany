using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughPlanTests
    {
        [TestCase("15", 15f)]
        [TestCase(" 2.5 ", 2.5f)]
        [TestCase("1e2", 100f)]
        [TestCase(null, PlaythroughPlan.DefaultTimeout)]
        [TestCase("", PlaythroughPlan.DefaultTimeout)]
        [TestCase("invalid", PlaythroughPlan.DefaultTimeout)]
        [TestCase("1,5", PlaythroughPlan.DefaultTimeout)]
        [TestCase("0", PlaythroughPlan.DefaultTimeout)]
        [TestCase("-1", PlaythroughPlan.DefaultTimeout)]
        [TestCase("NaN", PlaythroughPlan.DefaultTimeout)]
        [TestCase("Infinity", PlaythroughPlan.DefaultTimeout)]
        [TestCase("-Infinity", PlaythroughPlan.DefaultTimeout)]
        [TestCase("1e40", PlaythroughPlan.DefaultTimeout)]
        public void TimeoutAcceptsOnlyPositiveFiniteInvariantSeconds(string value, float expected) =>
            Assert.AreEqual(expected, PlaythroughPlan.ParseTimeout(value));

        [Test]
        public void InvalidTimeoutKeepsThePreviousCommandLineValue() =>
            Assert.AreEqual(30f, PlaythroughPlan.ParseTimeout("invalid", 30f));

        [TestCase(PlaythroughMilestone.Respawn)]
        [TestCase(PlaythroughMilestone.RampCandle)]
        [TestCase(PlaythroughMilestone.LakeCandle)]
        [TestCase(PlaythroughMilestone.Road)]
        public void ReturnRequestsRunningAfterRespawn(PlaythroughMilestone milestone)
        {
            var plan = new PlaythroughPlan(new[] { milestone });
            var observation = new PlaythroughObservation { Respawning = true };
            var command = plan.Tick(0.1f, in observation);
            Assert.IsFalse(command.Move || command.Run);
            observation.Respawning = false;
            command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Run);
            Assert.AreEqual(PlaythroughPlan.NeedsNavigation(milestone), command.Move);
        }

        [Test]
        public void OtherMilestonesDoNotRequestRunning()
        {
            foreach (PlaythroughMilestone milestone in System.Enum.GetValues(typeof(PlaythroughMilestone)))
            {
                if (milestone == PlaythroughMilestone.Respawn || milestone == PlaythroughMilestone.RampCandle
                    || milestone == PlaythroughMilestone.LakeCandle || milestone == PlaythroughMilestone.Road) continue;
                var plan = new PlaythroughPlan(new[] { milestone });
                var observation = new PlaythroughObservation();
                Assert.IsFalse(plan.Tick(0.1f, in observation).Run, milestone.ToString());
            }
        }

        [TestCase(PlaythroughMilestone.RampCandle)]
        [TestCase(PlaythroughMilestone.LakeCandle)]
        [TestCase(PlaythroughMilestone.Road)]
        public void ReturnWaitsInsideSafeZoneUntilWitchLeavesNextDestination(PlaythroughMilestone milestone)
        {
            var plan = new PlaythroughPlan(new[] { milestone });
            var observation = new PlaythroughObservation
            {
                Target = Vector3.forward * 30f, WitchPosition = Vector3.forward * 35f,
                InSafeZone = true, Respawning = true
            };
            plan.Tick(0.1f, in observation);
            observation.Respawning = false;
            observation.RespawnGraceRemaining = 7f;
            var command = plan.Tick(0.1f, in observation);
            Assert.IsFalse(command.Move || command.Run || command.Repath);
            observation.RespawnGraceRemaining = 3f;
            command = plan.Tick(4f, in observation);
            Assert.IsFalse(command.Move || command.Run || command.Repath);
            Assert.AreEqual(milestone, plan.Current);
            observation.WitchPosition = observation.Target + Vector3.forward * PlaythroughPlan.ReturnDepartureClearance;
            command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Move && command.Run && command.Repath);
            Assert.IsFalse(plan.Tick(0.1f, in observation).Repath);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void ReturnLeavesSafeZoneWhenGraceEndsEvenIfWitchStillBlocksDestination(float remaining)
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.LakeCandle });
            var observation = new PlaythroughObservation
            {
                Target = Vector3.forward * 30f, WitchPosition = Vector3.forward * 30f,
                InSafeZone = true, RespawnGraceRemaining = 0.01f
            };
            Assert.IsFalse(plan.Tick(0.1f, in observation).Move);
            observation.RespawnGraceRemaining = remaining;
            var command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Move && command.Run);
            Assert.IsFalse(command.Repath);
        }

        [Test]
        public void WitchNearPlayerDoesNotBlockDepartureWhenNextDestinationIsClear()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.RampCandle });
            var observation = new PlaythroughObservation
            {
                Target = Vector3.forward * 30f, WitchPosition = Vector3.forward * 5f,
                InSafeZone = true, RespawnGraceRemaining = 7f
            };
            var command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Move && command.Run);
        }

        [TestCase(false, 7f)]
        [TestCase(true, 0f)]
        public void ReturnDoesNotWaitOutsideSafeZoneOrWithoutRespawnGrace(bool safe, float grace)
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.LakeCandle });
            var observation = new PlaythroughObservation
            {
                InSafeZone = safe, RespawnGraceRemaining = grace
            };
            var command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Move && command.Run);
        }

        [Test]
        public void RespawnWaitDoesNotApplyOutsideReturn()
        {
            var plan = new PlaythroughPlan();
            var observation = new PlaythroughObservation { InSafeZone = true, RespawnGraceRemaining = 7f };
            var command = plan.Tick(0.1f, in observation);
            Assert.IsTrue(command.Move);
            Assert.IsFalse(command.Run);
        }

        [Test]
        public void RampCandleIsAStandaloneCheckpointAndRequiresTouchAndFullRecharge()
        {
            Assert.AreEqual(-1, PlaythroughPlan.ChallengeIndex(PlaythroughMilestone.RampCandle));
            Assert.AreEqual(ChallengeAction.None, PlaythroughPlan.Action(PlaythroughMilestone.RampCandle));
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.RampCandle, PlaythroughMilestone.LakeCandle });
            var observation = new PlaythroughObservation { AtTarget = true, Energy = SoulEnergy.Max };
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.RampCandle, plan.Current);
            observation.CheckpointReached = true;
            observation.Energy = SoulEnergy.Max - 2f;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.RampCandle, plan.Current);
            observation.Energy = SoulEnergy.Max;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.LakeCandle, plan.Current);
            Assert.AreEqual(1, plan.CompletedCount);
        }

        [Test]
        public void FullRouteRequiresMemoriesPowersHidingCaptureAndHomeInOrder()
        {
            var expected = new[]
            {
                PlaythroughMilestone.Memory1, PlaythroughMilestone.Ball, PlaythroughMilestone.Light,
                PlaythroughMilestone.Flash,
                PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide,
                PlaythroughMilestone.Memory2, PlaythroughMilestone.Challenge1Recharge,
                PlaythroughMilestone.Challenge1Candle1, PlaythroughMilestone.Challenge1Candle2, PlaythroughMilestone.Challenge1Cross,
                PlaythroughMilestone.Memory3, PlaythroughMilestone.Challenge2Recharge, PlaythroughMilestone.Challenge2Distract,
                PlaythroughMilestone.Challenge2Wait, PlaythroughMilestone.Challenge2Cross,
                PlaythroughMilestone.Memory4, PlaythroughMilestone.Challenge3Recharge, PlaythroughMilestone.Challenge3Entry,
                PlaythroughMilestone.Challenge3Vanish, PlaythroughMilestone.Challenge3Cross,
                PlaythroughMilestone.Challenge4Recharge, PlaythroughMilestone.Challenge4Candle1, PlaythroughMilestone.Challenge4Candle2,
                PlaythroughMilestone.Challenge4Distract, PlaythroughMilestone.Challenge4Wait,
                PlaythroughMilestone.Challenge4Vanish, PlaythroughMilestone.Challenge4Cross,
                PlaythroughMilestone.Memory5, PlaythroughMilestone.Capture, PlaythroughMilestone.Respawn,
                PlaythroughMilestone.RampCandle, PlaythroughMilestone.LakeCandle, PlaythroughMilestone.Road,
                PlaythroughMilestone.Manifest
            };
            var plan = new PlaythroughPlan();
            Assert.AreEqual(expected.Length, plan.Count);
            foreach (var milestone in expected)
            {
                Assert.AreEqual(milestone, plan.Current);
                var observation = CompletedObservation(milestone);
                plan.Tick(0.1f, in observation);
            }
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(expected.Length, plan.CompletedCount);
            Assert.AreEqual(0, plan.ExitCode);
        }

        [Test]
        public void ProximityAloneDoesNotCollectAMemory()
        {
            var plan = new PlaythroughPlan();
            var observation = new PlaythroughObservation { AtTarget = true };
            Assert.IsTrue(plan.Tick(0.1f, in observation).Interact);
            Assert.AreEqual(0, plan.CompletedCount);
            observation.MemoryFound = true;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.Ball, plan.Current);
        }

        [Test]
        public void DistractionWaitsForWitchToLeaveAndCrossingRequiresProof()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Challenge2Wait, PlaythroughMilestone.Challenge2Cross });
            var observation = new PlaythroughObservation { AtTarget = true, ObjectMoved = true };
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(0, plan.CompletedCount);
            observation.Distracted = true;
            plan.Tick(0.1f, in observation);
            plan.Tick(0.1f, in observation);
            Assert.IsFalse(plan.Finished);
            observation.ChallengeCompleted = true;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(0, plan.ExitCode);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaptureOrLostDistractionReturnsToRecharge(bool caught)
        {
            var plan = new PlaythroughPlan(new[]
            {
                PlaythroughMilestone.Challenge2Recharge, PlaythroughMilestone.Challenge2Distract,
                PlaythroughMilestone.Challenge2Wait, PlaythroughMilestone.Challenge2Cross
            });
            var observation = new PlaythroughObservation { AtTarget = true, CheckpointReached = true, Energy = 100f };
            plan.Tick(0.1f, in observation);
            observation.ObjectMoved = true;
            plan.Tick(0.1f, in observation);
            observation.Respawning = caught;
            observation.RetryChallenge = !caught;
            var command = plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.Challenge2Recharge, plan.Current);
            Assert.IsFalse(command.Move || command.MoveObject);
        }

        [Test]
        public void ReachingTheBallDoesNotProvePickup()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Ball });
            var observation = new PlaythroughObservation { AtTarget = true };
            Assert.IsTrue(plan.Tick(0.1f, in observation).Interact);
            Assert.IsFalse(plan.Finished);
            Assert.AreEqual(0, plan.CompletedCount);
            observation.BallCollected = true;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(0, plan.ExitCode);
        }

        [Test]
        public void MissingContentSkipsHidingAndReturnsCodeTwoOnlyAfterFinishing()
        {
            var plan = new PlaythroughPlan(new[]
            {
                PlaythroughMilestone.Ball, PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide,
                PlaythroughMilestone.Manifest
            });
            var observation = new PlaythroughObservation { AtTarget = true, MissingContent = "recogida real de la pelota" };
            plan.Tick(0.1f, in observation);
            Assert.IsFalse(plan.Finished);
            Assert.AreEqual(-1, plan.ExitCode);
            Assert.AreEqual(PlaythroughMilestone.Hide, plan.Current);
            observation.MissingContent = "escondite alcanzable por NavMesh desde la ruta";
            var command = plan.Tick(0.1f, in observation);
            Assert.IsFalse(command.Interact);
            Assert.IsFalse(command.Move);
            Assert.AreEqual(PlaythroughMilestone.Manifest, plan.Current);
            Assert.AreEqual(3, plan.CompletedCount);
            observation.MissingContent = null;
            observation.Credits = true;
            plan.Tick(0.1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(2, plan.ExitCode);
            Assert.AreEqual(4, plan.CompletedCount);
            CollectionAssert.AreEqual(new[] { "recogida real de la pelota", "escondite alcanzable por NavMesh desde la ruta" },
                plan.MissingContent);
            Assert.IsNull(plan.FailureReason);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConsoleErrorOrTimeoutTakesPriorityOverMissingContent(bool timeout)
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Ball, PlaythroughMilestone.Manifest }, 2f);
            var observation = new PlaythroughObservation { AtTarget = true, MissingContent = "recogida real de la pelota" };
            plan.Tick(0.1f, in observation);
            observation.MissingContent = null;
            observation.Credits = !timeout;
            if (!timeout) plan.RecordConsoleError("error de consola");
            plan.Tick(timeout ? 2f : 0.1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            Assert.AreEqual(1, plan.MissingContent.Count);
            StringAssert.Contains(timeout ? "tiempo límite" : "error de consola", plan.FailureReason);
        }

        [Test]
        public void TimeoutFailsWithCodeOneEvenWhenGameIsFrozen()
        {
            var plan = new PlaythroughPlan(timeout: 2f);
            var observation = new PlaythroughObservation { Frozen = true };
            plan.Tick(1f, in observation);
            Assert.IsFalse(plan.Finished);
            plan.Tick(1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            Assert.AreEqual(PlaythroughMilestone.Memory1, plan.Current);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [Test]
        public void EachMilestoneGetsItsOwnDeadline()
        {
            var plan = new PlaythroughPlan(timeout: 3f);
            var observation = new PlaythroughObservation { MemoryFound = true };
            plan.Tick(2.5f, in observation);
            observation.MemoryFound = false;
            plan.Tick(2.5f, in observation);
            Assert.IsFalse(plan.Finished);
            Assert.AreEqual(5f, plan.Elapsed);
        }

        [Test]
        public void LakeCandleTimesOutAfterOneHundredEightySeconds()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.LakeCandle });
            var observation = new PlaythroughObservation();
            plan.Tick(100f, in observation);
            Assert.IsFalse(plan.Finished);
            plan.Tick(79f, in observation);
            Assert.IsFalse(plan.Finished);
            plan.Tick(1.1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [Test]
        public void OtherMilestonesStillTimeOutAtNinetySeconds()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Road });
            var observation = new PlaythroughObservation();
            plan.Tick(89f, in observation);
            Assert.IsFalse(plan.Finished);
            plan.Tick(1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [TestCase(PlaythroughMilestone.Memory1, false)]
        [TestCase(PlaythroughMilestone.Road, true)]
        [TestCase(PlaythroughMilestone.Manifest, false)]
        public void CompletedRespawnRestartsOnlyTheCurrentDeadlineOnce(PlaythroughMilestone milestone, bool atRespawn)
        {
            var plan = new PlaythroughPlan(new[] { milestone }, timeout: 4f);
            var observation = new PlaythroughObservation();
            plan.Tick(2f, in observation);
            observation.Respawning = true;
            plan.Tick(1f, in observation);
            Assert.AreEqual(3f, plan.MilestoneElapsed);
            observation.Respawning = false;
            observation.AtRespawn = atRespawn;
            plan.Tick(1f, in observation);
            Assert.IsFalse(plan.Finished);
            Assert.AreEqual(0f, plan.MilestoneElapsed);
            Assert.AreEqual(4f, plan.Elapsed);
            Assert.AreEqual(milestone, plan.Current);
            Assert.AreEqual(0, plan.CompletedCount);
            plan.Tick(3.5f, in observation);
            Assert.IsFalse(plan.Finished);
            Assert.AreEqual(3.5f, plan.MilestoneElapsed);
            plan.Tick(0.5f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [Test]
        public void LakeReturnRepathsOnceAfterEachRespawnEvenIfTheGameIsFrozen()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.LakeCandle });
            var observation = new PlaythroughObservation();
            plan.Tick(0f, in observation);
            for (int capture = 0; capture < 2; capture++)
            {
                observation.Respawning = true;
                Assert.IsFalse(plan.Tick(0.1f, in observation).Repath);
                observation.Position += Vector3.back * 10f;
                Assert.IsFalse(plan.Tick(0.1f, in observation).Repath);
                observation.Respawning = false;
                observation.Frozen = true;
                Assert.IsFalse(plan.Tick(0.1f, in observation).Repath);
                observation.Frozen = false;
                var command = plan.Tick(0.1f, in observation);
                Assert.IsTrue(command.Move);
                Assert.IsTrue(command.Repath);
                Assert.IsFalse(plan.Tick(0.1f, in observation).Repath);
                Assert.AreEqual(PlaythroughMilestone.LakeCandle, plan.Current);
                Assert.IsFalse(plan.Finished);
            }
        }

        [Test]
        public void ReturnResumesNavigationAfterRespawnAtANewerFamilyCandle()
        {
            var checkpoint = new RespawnPoint(new Vector3(230f, 2f, 72f));
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Respawn, PlaythroughMilestone.Road });
            var observation = new PlaythroughObservation { Respawning = true };
            plan.Tick(0.1f, in observation);
            checkpoint.Touch(new Candle(CandleKind.Family, new Vector3(190f, 0f, 76f)));
            observation.Position = new Vector3(190f, 0.2f, 75f);
            observation.AtRespawn = checkpoint.IsAtArrival(observation.Position, 1f, 0.35f);
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.Respawn, plan.Current);
            observation.Respawning = false;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.Road, plan.Current);
            Assert.IsTrue(plan.Tick(0.1f, in observation).Move);
        }

        [TestCase(PlaythroughMilestone.Road, PlaythroughPlan.DefaultTimeout)]
        [TestCase(PlaythroughMilestone.LakeCandle, PlaythroughPlan.LakeCandleTimeout)]
        [TestCase(PlaythroughMilestone.Challenge1Recharge, PlaythroughPlan.DefaultTimeout)]
        public void FourthRespawnKeepsTheDeadlineAndRepeatedCapturesEventuallyFail(PlaythroughMilestone milestone, float deadline)
        {
            var plan = new PlaythroughPlan(new[] { milestone });
            var observation = new PlaythroughObservation();
            for (int capture = 0; capture < 3; capture++)
            {
                plan.Tick(20f, in observation);
                observation.Respawning = true;
                plan.Tick(1f, in observation);
                observation.Respawning = false;
                plan.Tick(1f, in observation);
                Assert.AreEqual(0f, plan.MilestoneElapsed);
            }
            observation.Respawning = true;
            plan.Tick(1f, in observation);
            observation.Respawning = false;
            Assert.IsTrue(plan.Tick(1f, in observation).Repath);
            Assert.AreEqual(2f, plan.MilestoneElapsed);
            while (!plan.Finished)
            {
                observation.Respawning = !observation.Respawning;
                plan.Tick(1f, in observation);
                Assert.LessOrEqual(plan.Elapsed, 66f + deadline);
            }
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [Test]
        public void CompletingAMilestoneRenewsTheThreeRespawnRestarts()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Road, PlaythroughMilestone.Manifest }, timeout: 5f);
            var observation = new PlaythroughObservation();
            for (int milestone = 0; milestone < 2; milestone++)
            {
                for (int capture = 0; capture < 3; capture++)
                {
                    observation.Respawning = true;
                    plan.Tick(1f, in observation);
                    observation.Respawning = false;
                    plan.Tick(1f, in observation);
                    Assert.AreEqual(0f, plan.MilestoneElapsed);
                }
                observation.AtTarget = milestone == 0;
                observation.Credits = milestone == 1;
                plan.Tick(0f, in observation);
            }
            Assert.AreEqual(0, plan.ExitCode);
        }

        [Test]
        public void ReachingTheRespawnPointWithoutCaptureDoesNotRestartTheDeadline()
        {
            var plan = new PlaythroughPlan(timeout: 2f);
            var observation = new PlaythroughObservation();
            plan.Tick(1f, in observation);
            observation.AtRespawn = true;
            plan.Tick(1f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(2f, plan.MilestoneElapsed);
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("tiempo límite", plan.FailureReason);
        }

        [Test]
        public void MovingLessThanOneMeterForThreeSecondsRequestsOneRepath()
        {
            var plan = new PlaythroughPlan();
            var observation = new PlaythroughObservation();
            plan.Tick(0f, in observation);
            observation.Position = Vector3.right * 0.9f;
            Assert.IsFalse(plan.Tick(2.9f, in observation).Repath);
            Assert.IsTrue(plan.Tick(0.11f, in observation).Repath);
            Assert.IsFalse(plan.Tick(0.01f, in observation).Repath);
        }

        [Test]
        public void OneMeterOfProgressResetsTheStuckClock()
        {
            var plan = new PlaythroughPlan();
            var observation = new PlaythroughObservation();
            plan.Tick(0f, in observation);
            plan.Tick(2.9f, in observation);
            observation.Position = Vector3.forward;
            Assert.IsFalse(plan.Tick(0.2f, in observation).Repath);
            Assert.IsFalse(plan.Tick(2.9f, in observation).Repath);
        }

        [Test]
        public void WaitingForAnInteractionDoesNotCountAsStuckOrHoldTheButton()
        {
            var plan = new PlaythroughPlan();
            var observation = new PlaythroughObservation { AtTarget = true };
            Assert.IsTrue(plan.Tick(0.01f, in observation).Interact);
            Assert.IsFalse(plan.Tick(0.01f, in observation).Interact);
            Assert.IsFalse(plan.Tick(4f, in observation).Repath);
        }

        [Test]
        public void CaptureFallsBackOnlyOnceAfterFortySecondsAndRequiresCompletedRespawn()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Capture, PlaythroughMilestone.Respawn });
            var observation = new PlaythroughObservation { AtTarget = true };
            var command = plan.Tick(39f, in observation);
            Assert.IsFalse(command.ForceRespawn);
            Assert.IsFalse(command.Move);
            Assert.IsTrue(command.ToggleLight);
            Assert.IsTrue(plan.Tick(1f, in observation).ForceRespawn);
            Assert.IsFalse(plan.Tick(1f, in observation).ForceRespawn);
            Assert.IsFalse(plan.Finished);
            observation.Respawning = true;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(PlaythroughMilestone.Respawn, plan.Current);
            observation.AtRespawn = true;
            plan.Tick(0.1f, in observation);
            Assert.IsFalse(plan.Finished);
            observation.Respawning = false;
            observation.AtRespawn = false;
            plan.Tick(0.1f, in observation);
            Assert.IsFalse(plan.Finished);
            observation.AtRespawn = true;
            plan.Tick(0.1f, in observation);
            Assert.AreEqual(0, plan.ExitCode);
        }

        [Test]
        public void FirstConsoleErrorTurnsAnOtherwiseCompleteRunIntoFailure()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Manifest });
            plan.RecordConsoleError("primer error");
            plan.RecordConsoleError("segundo error");
            Assert.IsFalse(plan.Finished);
            var observation = new PlaythroughObservation { Credits = true, Frozen = true };
            plan.Tick(1f, in observation);
            Assert.AreEqual(1, plan.ExitCode);
            StringAssert.Contains("primer error", plan.FailureReason);
            StringAssert.DoesNotContain("segundo error", plan.FailureReason);
        }

        [Test]
        public void EndingWaitsForCreditsThenReturnsCodeZeroDespitePausedGameplay()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Manifest });
            var observation = new PlaythroughObservation { AtTarget = true };
            Assert.IsTrue(plan.Tick(0.1f, in observation).Interact);
            observation.Frozen = true;
            plan.Tick(EndingRules.ApproachSeconds, in observation);
            Assert.IsFalse(plan.Finished);
            plan.Tick(EndingRules.FadeSeconds, in observation);
            Assert.IsFalse(plan.Finished);
            observation.Credits = true;
            plan.Tick(EndingRules.TextSeconds, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.AreEqual(0, plan.ExitCode);
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void NavigationInputMatchesPlayerCameraRelativeMovement(float yaw)
        {
            var direction = new Vector3(0.6f, 0f, -0.8f);
            Vector2 input = PlaythroughNavigation.CameraInput(direction, yaw);
            Assert.Less(Vector3.Distance(direction, PlayerMotion.CameraRelative(input, yaw)), 0.0001f);
        }

        [TestCase("-playthrough", false)]
        [TestCase("-motiondemo", true)]
        [TestCase("-playthrough-timeout", true)]
        [TestCase("", true)]
        public void VideoCaptureLeavesProcessExitToPlaythrough(string mode, bool expected)
        {
            Assert.AreEqual(expected, VideoCapture.ShouldQuit(new[] { "Tiffany", "-capturevideo", "frames", mode }));
            Assert.AreEqual(expected, VideoCapture.ShouldQuit(new[] { "Tiffany", mode, "-capturevideo", "frames" }));
        }

        static PlaythroughObservation CompletedObservation(PlaythroughMilestone milestone) => new PlaythroughObservation
        {
            AtTarget = true, MemoryFound = true, BallCollected = true, LightOn = true, Flashing = true,
            Hidden = milestone == PlaythroughMilestone.Hide, CandleLit = true, ObjectMoved = true, Vanished = true,
            CheckpointReached = true, Energy = SoulEnergy.Max, Respawning = milestone == PlaythroughMilestone.Capture,
            AtRespawn = true, Credits = true, ChallengeCompleted = true, Distracted = true
        };

        [Test]
        public void BotWalksWithItsLightAndTurnsItOffUnderTheWitchFire()
        {
            var o = new PlaythroughObservation { Energy = 80f, WitchState = WitchState.Flying, WitchPosition = new Vector3(60f, 20f, 0f) };
            Assert.IsTrue(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o));
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, false, in o), "Quieta no la necesita.");
            o.WitchPosition = new Vector3(20f, 20f, 0f);
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "La apaga con el fuego a menos de 25 m.");
            o.WitchState = WitchState.Stalking;
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "Huye a oscuras de la bruja cercana en tierra.");
            o.WitchPosition = new Vector3(60f, 20f, 0f);
            o.Energy = 29f;
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "Ahorra energía bajo 30.");
        }

        [TestCase(WitchState.Stalking)]
        [TestCase(WitchState.WindUp)]
        [TestCase(WitchState.Sprinting)]
        [TestCase(WitchState.Recovering)]
        [TestCase(WitchState.Stunned)]
        [TestCase(WitchState.Caught)]
        [TestCase(WitchState.Suspicious)]
        public void WalkingLightStaysOffWithinThirtyHorizontalMetersOfTheGroundedWitch(WitchState state)
        {
            var o = new PlaythroughObservation
            {
                Energy = 80f, WitchState = state, Position = new Vector3(10f, 2f, -4f),
                WitchPosition = new Vector3(39f, 60f, -4f)
            };
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "Huye a oscuras a menos de 30 m.");
            o.WitchPosition.x = 40f;
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "Incluye los 30 m e ignora el desnivel.");
            o.WitchPosition.x = 40.01f;
            Assert.IsTrue(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "Puede alumbrar fuera de los 30 m.");
        }

        [TestCase(WitchState.Flying, false)]
        [TestCase(WitchState.Landing, true)]
        [TestCase(WitchState.TakingOff, true)]
        public void AirborneStatesKeepTheirExistingWalkingLightRule(WitchState state, bool lightAtTwentyMeters)
        {
            var o = new PlaythroughObservation { Energy = 80f, WitchState = state, WitchPosition = new Vector3(30f, 20f, 0f) };
            Assert.IsTrue(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "El radio terrestre no se aplica en vuelo.");
            o.WitchPosition.x = 25f;
            Assert.IsTrue(PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o), "El fuego solo obliga a apagarla por debajo de 25 m.");
            o.WitchPosition.x = 20f;
            Assert.AreEqual(lightAtTwentyMeters, PlaythroughPlan.WalksWithLight(PlaythroughMilestone.Memory2, true, in o));
        }

        [TestCase(PlaythroughMilestone.Memory2)]
        [TestCase(PlaythroughMilestone.LakeCandle)]
        [TestCase(PlaythroughMilestone.Road)]
        [TestCase(PlaythroughMilestone.Manifest)]
        public void WalkingTurnsTheLightOffNearTheGroundedWitchAndBackOnAtADistance(PlaythroughMilestone milestone)
        {
            var plan = new PlaythroughPlan(new[] { milestone });
            var o = new PlaythroughObservation
            {
                Energy = 80f, LightOn = true, WitchState = WitchState.Stalking,
                WitchPosition = new Vector3(30f, 0f, 0f)
            };
            var command = plan.Tick(0.1f, in o);
            Assert.IsTrue(command.Move);
            Assert.IsTrue(command.ToggleLight, "Apaga la luz para seguir huyendo a oscuras.");
            o.LightOn = false;
            Assert.IsFalse(plan.Tick(0.3f, in o).ToggleLight, "La mantiene apagada mientras la bruja sigue cerca.");
            o.WitchPosition.x = 30.01f;
            Assert.IsTrue(plan.Tick(0.3f, in o).ToggleLight, "Vuelve a alumbrar cuando la bruja queda lejos.");
        }

        [TestCase(PlaythroughMilestone.Hide)]
        [TestCase(PlaythroughMilestone.LeaveHide)]
        [TestCase(PlaythroughMilestone.Vanish)]
        [TestCase(PlaythroughMilestone.Challenge2Cross)]
        public void BotKeepsTheScriptedLightOnHidingVanishingAndChallenges(PlaythroughMilestone milestone)
        {
            var o = new PlaythroughObservation { Energy = 80f, WitchPosition = new Vector3(100f, 20f, 0f) };
            Assert.IsFalse(PlaythroughPlan.WalksWithLight(milestone, true, in o));
        }

        [Test]
        public void FlashMilestoneHoldsThroughChargeAndReleasesAfterBurst()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Flash });
            var observation = new PlaythroughObservation { AtTarget = true, Energy = SoulEnergy.Max };
            var charge = new FlashCharge();
            for (int i = 0; i < 12; i++)
            {
                var command = plan.Tick(0.25f, in observation);
                Assert.IsTrue(command.LightHeld);
                Assert.IsFalse(command.ToggleLight);
                var result = charge.Tick(0.25f, command.LightHeld, true);
                Assert.AreEqual(i == 11 ? FlashChargeResult.Burst : FlashChargeResult.None, result);
            }
            observation.Flashing = true;
            var release = plan.Tick(0.01f, in observation);
            Assert.IsTrue(plan.Finished);
            Assert.IsFalse(release.LightHeld);
            Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.01f, release.LightHeld, true));
        }

        [Test]
        public void WalkingLightTogglesTheLampOnTheWayToAMemory()
        {
            var plan = new PlaythroughPlan(new[] { PlaythroughMilestone.Memory2 });
            var o = new PlaythroughObservation { Energy = 80f, WitchPosition = new Vector3(100f, 20f, 0f) };
            Assert.IsTrue(plan.Tick(0.1f, in o).ToggleLight);
        }
    }
}
