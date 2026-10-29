using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PlaythroughDodgeTests
    {
        static readonly Vector3 Witch = Vector3.forward * 10f;

        [TestCase(WitchState.Stalking, 10f, false)]
        [TestCase(WitchState.Sprinting, 10f, false)]
        [TestCase(WitchState.Recovering, 10f, false)]
        [TestCase(WitchState.WindUp, 12.1f, false)]
        [TestCase(WitchState.WindUp, 12f, true)]
        [TestCase(WitchState.WindUp, 3f, true)]
        public void StartsOnlyOnTheWarningWithinTwelveMeters(WitchState state, float distance, bool dodges)
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.AreEqual(dodges, dodge.Tick(0.02f, Vector3.zero, Vector3.forward, state, Vector3.forward * distance, true));
            Assert.AreEqual(dodges, dodge.Started);
        }

        [Test]
        public void DoesNotStartWhileTheBotIsNotNavigating()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsFalse(dodge.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, false));
            Assert.IsTrue(dodge.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
        }

        [Test]
        public void MovesSidewaysAndNeverTowardTheWitchPreferringTheSideItIsHeading()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Vector3 heading = new Vector3(-1f, 0f, 1f).normalized;
            Assert.IsTrue(dodge.Tick(0.02f, Vector3.zero, heading, WitchState.WindUp, Witch, true));
            Assert.AreEqual(1f, dodge.Direction.magnitude, 1e-4f);
            Assert.AreEqual(0f, Vector3.Dot(dodge.Direction, Witch.normalized), 1e-4f);
            Assert.Greater(Vector3.Dot(dodge.Direction, heading), 0f);
        }

        [Test]
        public void UsesTheOtherSideWhenTheFirstHasNoNavMeshAndBacksAwayWhenNeitherHas()
        {
            Vector3 blocked = Vector3.right;
            var oneSide = new PlaythroughDodge((_, direction) => Vector3.Dot(direction, blocked) < 0.5f);
            oneSide.Tick(0.02f, Vector3.zero, new Vector3(1f, 0f, 1f).normalized, WitchState.WindUp, Witch, true);
            Assert.Less(Vector3.Distance(Vector3.left, oneSide.Direction), 1e-4f);

            var none = new PlaythroughDodge((_, _) => false);
            none.Tick(0.02f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true);
            Assert.Less(Vector3.Distance(Vector3.back, none.Direction), 1e-4f, "Sin lado libre retrocede, lejos de la bruja.");
        }

        [Test]
        public void LastsOneSecondThroughTheLungeThenRequestsRepathOnce()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Vector3 direction = dodge.Direction;
            Assert.IsTrue(dodge.Tick(0.5f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, false));
            Assert.IsTrue(dodge.Tick(0.45f, Vector3.right, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.AreEqual(direction, dodge.Direction);
            Assert.IsFalse(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.right, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsTrue(dodge.Repath);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.right, Vector3.forward, WitchState.Recovering, Witch, true));
            Assert.IsFalse(dodge.Repath);
        }

        [Test]
        public void DodgesEachWarningOnlyOnce()
        {
            var dodge = new PlaythroughDodge((_, _) => true);
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Assert.IsFalse(dodge.Tick(1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Assert.IsFalse(dodge.Started);
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Sprinting, Witch, true));
            Assert.IsFalse(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.Stalking, Witch, true));
            Assert.IsTrue(dodge.Tick(0.1f, Vector3.zero, Vector3.forward, WitchState.WindUp, Witch, true));
            Assert.IsTrue(dodge.Started);
        }
    }
}
