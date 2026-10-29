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
                PlaythroughMilestone.LakeCandle, PlaythroughMilestone.Road, PlaythroughMilestone.Manifest
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
    }
}
