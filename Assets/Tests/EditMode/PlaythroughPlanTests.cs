using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughPlanTests
    {
        [Test]
        public void FullRouteRequiresMemoriesPowersHidingCaptureAndHomeInOrder()
        {
            var expected = new[]
            {
                PlaythroughMilestone.Memory1, PlaythroughMilestone.Ball, PlaythroughMilestone.Light,
                PlaythroughMilestone.Flash, PlaythroughMilestone.Hide, PlaythroughMilestone.LeaveHide,
                PlaythroughMilestone.Memory2, PlaythroughMilestone.Recharge2, PlaythroughMilestone.Kindle,
                PlaythroughMilestone.Memory3, PlaythroughMilestone.Recharge3, PlaythroughMilestone.MoveObject,
                PlaythroughMilestone.Memory4, PlaythroughMilestone.Recharge4, PlaythroughMilestone.Vanish,
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

        static PlaythroughObservation CompletedObservation(PlaythroughMilestone milestone) => new PlaythroughObservation
        {
            AtTarget = true, MemoryFound = true, TylerVisible = true, LightOn = true, Flashing = true,
            Hidden = milestone == PlaythroughMilestone.Hide, CandleLit = true, ObjectMoved = true, Vanished = true,
            CheckpointReached = true, Energy = SoulEnergy.Max, Respawning = milestone == PlaythroughMilestone.Capture,
            AtRespawn = true, Credits = true
        };
    }
}
