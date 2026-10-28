using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class TylerMotionTests
    {
        [TestCase(0f, 0f, 1f, true)]
        [TestCase(0f, 10f, 1f, true)]
        [TestCase(0.09f, 0f, 1f, true)]
        [TestCase(0.1f, 0f, 1f, false)]
        [TestCase(0.06f, 0f, 0.5f, false)]
        [TestCase(5f, 0f, 0f, true)]
        [TestCase(5f, 0f, -1f, true)]
        public void StillnessUsesHorizontalSpeedAndHandlesNonPositiveTime(float x, float y, float dt, bool expected)
        {
            Assert.AreEqual(expected, TylerMotion.IsStill(Vector3.zero, new Vector3(x, y, 0f), dt));
        }

        [TestCase(10f, 2f, 3f)]
        [TestCase(2f, 2f, 7f)]
        [TestCase(1f, 1f, 7f)]
        [TestCase(0f, 0f, 7f)]
        public void FollowKeepsPlayerHeightUntilAlreadyClose(float distance, float expectedX, float expectedY)
        {
            var player = new Vector3(0f, 3f, 0f);
            var tyler = new Vector3(distance, 7f, 0f);

            Assert.AreEqual(new Vector3(expectedX, expectedY, 0f), TylerMotion.FollowTarget(tyler, player));
        }

        [TestCase(0f)]
        [TestCase(5f)]
        public void FollowAtSamePointStaysPut(float height)
        {
            var position = new Vector3(3f, height, -4f);
            Assert.AreEqual(position, TylerMotion.FollowTarget(position, position));
        }

        [TestCase(0f, 0f)]
        [TestCase(2f, 2f)]
        [TestCase(5f, 5f)]
        [TestCase(12f, 5f)]
        public void GuideLimitsHorizontalLeadFromPlayer(float distance, float expectedZ)
        {
            var player = new Vector3(2f, 3f, 0f);
            var memory = new Vector3(2f, 20f, distance);

            Assert.AreEqual(new Vector3(2f, 3f, expectedZ),
                TylerMotion.GuideTarget(new Vector3(-10f, 0f, -10f), player, memory));
        }

        [TestCase(3f, 4f, 1.2f, 1.6f)]
        [TestCase(-3f, -4f, -1.2f, -1.6f)]
        public void TargetsPreserveHorizontalDirection(float x, float z, float followX, float followZ)
        {
            var target = new Vector3(x, 0f, z);
            Assert.That(Vector3.Distance(new Vector3(followX, 0f, followZ),
                TylerMotion.FollowTarget(target, Vector3.zero)), Is.LessThan(0.0001f));
            Assert.AreEqual(target, TylerMotion.GuideTarget(Vector3.zero, Vector3.zero, target));
        }

        [TestCase(0f, PlayerMotion.WalkSpeed)]
        [TestCase(3.99f, PlayerMotion.WalkSpeed)]
        [TestCase(4f, PlayerMotion.WalkSpeed)]
        [TestCase(4.01f, PlayerMotion.RunSpeed + TylerMotion.SpeedMargin)]
        public void SpeedCatchesRunningPlayerBeyondFourMeters(float distance, float expected)
        {
            Assert.AreEqual(expected, TylerMotion.MoveSpeed(distance));
        }

        [TestCase(TylerReaction.None, false, false, false, false)]
        [TestCase(TylerReaction.Guide, false, false, false, false)]
        [TestCase(TylerReaction.Whine, true, true, false, true)]
        [TestCase(TylerReaction.Growl, true, false, true, true)]
        public void ReactionSelectsPose(TylerReaction reaction, bool stopped, bool lookUp, bool crouched, bool faceWitch)
        {
            TylerPose pose = TylerPoses.For(reaction);
            Assert.AreEqual(stopped, pose.Stopped);
            Assert.AreEqual(lookUp, pose.LookUp);
            Assert.AreEqual(crouched, pose.Crouched);
            Assert.AreEqual(faceWitch, pose.FaceWitch);
        }

        [TestCase(TylerReaction.Whine, 1f, 5f, "Whine")]
        [TestCase(TylerReaction.Whine, 0f, 5f, "Whine")]
        [TestCase(TylerReaction.Growl, 1f, 5f, "Growl")]
        [TestCase(TylerReaction.Growl, 0f, 5f, "Growl")]
        [TestCase(TylerReaction.None, 0.11f, 5f, "Walk")]
        [TestCase(TylerReaction.Guide, 0.11f, 5f, "Walk")]
        [TestCase(TylerReaction.None, 0.1f, 4.01f, "Sit")]
        [TestCase(TylerReaction.Guide, 0f, 4.01f, "Sit")]
        [TestCase(TylerReaction.None, 0f, 0f, "Idle")]
        [TestCase(TylerReaction.Guide, 0f, 0f, "Idle")]
        [TestCase(TylerReaction.None, 0.1f, 4f, "Idle")]
        [TestCase(TylerReaction.None, 0f, 4f, "Idle")]
        public void AnimationPrioritizesReactionThenMovementThenIdleTime(
            TylerReaction reaction, float speed, float idleSeconds, string expected)
        {
            Assert.AreEqual(expected, TylerAnimation.StateFor(reaction, speed, idleSeconds));
        }
    }
}
