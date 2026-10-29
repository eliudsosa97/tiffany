using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class LegFacingTests
    {
        const float Dt = 1f / 60f;

        [TestCase(90f, 90f, -85f, false)]
        [TestCase(-90f, -90f, 85f, false)]
        [TestCase(150f, -30f, 30f, true)]
        [TestCase(-150f, 30f, -30f, true)]
        [TestCase(180f, 0f, 0f, true)]
        [TestCase(450f, 90f, -85f, false)]
        [TestCase(-210f, -30f, 30f, true)]
        public void LegsFollowTravelAndTorsoReturnsTowardAim(float angle, float legs, float twist, bool backward)
        {
            var state = new LegFacing();
            Settle(ref state, angle);
            Assert.AreEqual(legs, state.LegsOffset, 0.01f);
            Assert.AreEqual(twist, state.Twist, 0.01f);
            Assert.AreEqual(backward, state.Backward);
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void BackwardEntersAbove120AndExitsBelow100(float sign)
        {
            var state = new LegFacing();
            foreach (var (angle, backward) in new[] { (120f, false), (120.1f, true), (110f, true), (100f, true), (99.9f, false) })
            {
                state.Advance(-angle * sign, Vector3.forward, false, Dt);
                Assert.AreEqual(backward, state.Backward, angle.ToString());
            }
        }

        [TestCase(true, 1f)]
        [TestCase(false, 0f)]
        [TestCase(false, TiffanyAnimation.MoveSpeed)]
        public void RunningOrNoHorizontalMovementSmoothlyReturnsToZero(bool running, float speed)
        {
            var state = new LegFacing();
            Settle(ref state, 150f);
            state.Advance(-150f, new Vector3(0f, 8f, speed), running, Dt);
            Assert.IsFalse(state.Backward);
            Assert.That(state.LegsOffset, Is.InRange(-30f, -0.01f));
            Settle(ref state, 150f, running, speed);
            Assert.AreEqual(0f, state.LegsOffset, 0.01f);
            Assert.AreEqual(0f, state.Twist, 0.01f);
        }

        [TestCase(1f, 121f, -59f)]
        [TestCase(-1f, 121f, -59f)]
        [TestCase(1f, -115f, -115f)]
        [TestCase(-1f, -115f, -115f)]
        public void LegTransitionsCrossTheFrontWithoutPassingBehindTheBody(float sign, float angle, float target)
        {
            var state = new LegFacing();
            Settle(ref state, 115f * sign);
            for (int i = 0; i < 60; i++)
            {
                float previous = state.LegsOffset;
                state.Advance(-angle * sign, Vector3.forward, false, Dt);
                Assert.LessOrEqual(Mathf.Abs(state.LegsOffset), 130f);
                Assert.LessOrEqual(Mathf.Abs(state.LegsOffset - previous), LegFacing.MaxLegsSpeed * Dt);
                Assert.LessOrEqual(Mathf.Abs(state.Twist), LegFacing.MaxTwist);
            }
            Assert.AreEqual(target * sign, state.LegsOffset, 0.01f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimePreservesTheEntireState(float dt)
        {
            var state = new LegFacing();
            state.Advance(-150f, Vector3.forward, false, Dt);
            var previous = state;
            state.Advance(0f, Vector3.zero, true, dt);
            Assert.AreEqual(previous, state);
        }

        static void Settle(ref LegFacing state, float angle, bool running = false, float speed = 1f)
        {
            for (int i = 0; i < 60; i++) state.Advance(-angle, Vector3.forward * speed, running, Dt);
        }
    }
}
