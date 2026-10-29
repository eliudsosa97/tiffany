using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughDodgeTests
    {
        static readonly Vector3 Witch = Vector3.forward * 10f;

        [TestCase(WitchState.Stalking, 10f, false)]
        [TestCase(WitchState.WindUp, 10f, false)]
        [TestCase(WitchState.Recovering, 10f, false)]
        [TestCase(WitchState.Sprinting, 12.1f, false)]
        [TestCase(WitchState.Sprinting, 12f, true)]
        [TestCase(WitchState.Sprinting, 3f, true)]
        public void StartsOnlyOnTheSprintWithinTwelveMeters(WitchState state, float distance, bool dodges)
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.AreEqual(dodges, dodge.Tick(0.02f, Vector3.zero, Vector3.forward, state, Vector3.forward * distance, true));
            Assert.AreEqual(dodges, dodge.Started);
        }

        [Test]
        public void DoesNotStartWhileTheBotIsNotNavigating()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsFalse(dodge.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, false));
            Assert.IsFalse(dodge.Started);
            Assert.IsTrue(dodge.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
        }

        [Test]
        public void MovesSidewaysAndNeverTowardTheWitchPreferringTheSideItIsHeading()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Vector3 heading = new Vector3(-1f, 0f, 1f).normalized;
            Assert.IsTrue(dodge.Tick(0.02f, Vector3.zero, heading, WitchState.Sprinting, Witch, true));
            Assert.AreEqual(1f, dodge.Direction.magnitude, 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(dodge.Direction, Witch.normalized), 1e-4f);
            Assert.Greater(Vector3.Dot(dodge.Direction, heading), 0f);
        }

        [Test]
        public void UsesTheOtherSideWhenTheFirstHasNoNavMeshAndBacksAwayWhenNeitherHas()
        {
            Vector3 blocked = Vector3.right;
            var oneSide = new PlaythroughDodge((_, direction) => Vector3.Dot(direction, blocked) < 0.5f);
            oneSide.Tick(0.02f, Vector3.zero, new Vector3(1f, 0f, 1f).normalized, WitchState.Sprinting, Witch, true);
            Assert.Less(Vector3.Distance(Vector3.left, oneSide.Direction), 1e-4f);

            var none = new PlaythroughDodge((_, _) => false);
            none.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true);
            Assert.Less(Vector3.Distance(Vector3.back, none.Direction), 1e-4f, "Sin lado libre retrocede, lejos de la bruja.");
        }

        [Test]
        public void LastsThroughALongerSprintThenRequestsRepathOnce()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Vector3 direction = dodge.Direction;
            Assert.IsTrue(dodge.Tick(0.5f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, false));
            Assert.IsTrue(dodge.Tick(0.75f, Vector3.right, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsTrue(dodge.Tick(0.5f, Vector3.right, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.AreEqual(direction, dodge.Direction);
            Assert.IsFalse(dodge.Started);
            Assert.IsFalse(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.right, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsTrue(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.right, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsFalse(dodge.Repath);
        }

        [Test]
        public void LastsAtLeastOnePointTwoSecondsWhenTheSprintEndsEarly()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsTrue(dodge.Tick(0f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsTrue(dodge.Tick(1.19f, Vector3.zero, Vector3.forward, WitchState.Recovering, Witch, false));
            Assert.IsFalse(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsTrue(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsFalse(dodge.Repath);
        }

        [Test]
        public void DodgesEachSprintOnlyOnce()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsTrue(dodge.Started);
            Assert.IsTrue(dodge.Tick(2f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsFalse(dodge.Started);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Stalking, Witch, true));
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsTrue(dodge.Started);
        }

        [Test]
        public void MovesPerpendicularToTheSprintLineLockedAfterWindUp()
        {
            var profile = new WitchProfile { GroundChase = true };
            var brain = new WitchBrain(profile, null);
            var dodge = new PlaythroughDodge((_, _) => true);
            var p = new WitchPerception { WitchPosition = Vector3.forward * 8f, DistanceToPlayer = 8f };
            WitchDecision warning = brain.Tick(0f, in p);
            Assert.AreEqual(WitchState.WindUp, warning.State);

            bool dodging = dodge.Tick(0f, p.PlayerPosition, Vector3.forward, warning.State, p.WitchPosition, true);
            p.PlayerPosition += (dodging ? dodge.Direction : Vector3.forward) * PlayerMotion.RunSpeed * profile.WindUpDuration;
            p.DistanceToPlayer = Vector3.Distance(p.PlayerPosition, p.WitchPosition);
            WitchDecision sprint = brain.Tick(profile.WindUpDuration, in p);
            Assert.AreEqual(WitchState.Sprinting, sprint.State);
            Vector3 sprintLine = (sprint.MoveTarget - p.WitchPosition).normalized;

            Assert.IsTrue(dodge.Tick(profile.WindUpDuration, p.PlayerPosition, Vector3.forward,
                sprint.State, p.WitchPosition, true));
            Assert.AreEqual(0f, Vector3.Dot(dodge.Direction, sprintLine), 1e-4f,
                "La esquiva debe ser perpendicular a la línea fijada al terminar el aviso.");
        }
    }
}
