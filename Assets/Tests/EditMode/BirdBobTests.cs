using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class BirdBobTests
    {
        const float Dt = 1f / 60f;

        [TestCase(0)]
        [TestCase(713)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void GesturesStaySmallAndAlternateWithLongStillness(int seed)
        {
            var bob = new BirdBob(seed);
            int still = 0, moving = 0, hops = 0, pecks = 0, turns = 0;
            for (int i = 0; i < 18000; i++)
            {
                var pose = bob.Tick(Dt);
                Assert.Less(pose.Offset.magnitude, 0.03f);
                Assert.Less(Quaternion.Angle(Quaternion.identity, pose.Rotation), 25f);
                Assert.That(bob.Duration, bob.Phase == BirdBob.Motion.Still ? Is.InRange(2f, 6f) : Is.InRange(0.2f, 0.5f));
                if (bob.Phase == BirdBob.Motion.Still)
                {
                    Assert.AreEqual(Vector3.zero, pose.Offset);
                    Assert.AreEqual(Quaternion.identity, pose.Rotation);
                    still++;
                }
                else
                {
                    moving++;
                    if (bob.Phase == BirdBob.Motion.Hop && pose.Offset.y > 0f) hops++;
                    if (bob.Phase == BirdBob.Motion.Peck) pecks++;
                    if (bob.Phase == BirdBob.Motion.Turn) turns++;
                }
            }
            Assert.Greater(still, moving * 3);
            Assert.Greater(hops, 0);
            Assert.Greater(pecks, 0);
            Assert.Greater(turns, 0);
        }

        [Test]
        public void SameSeedAndTimesProduceTheSamePosesWithoutGlobalRandomState()
        {
            var first = new BirdBob(71);
            var second = new BirdBob(71);
            var unrelated = new BirdBob(72);
            Assert.AreNotEqual(first.Duration, unrelated.Duration);
            for (int i = 0; i < 10000; i++)
            {
                float dt = i % 3 == 0 ? 0.23f : Dt;
                var expected = first.Tick(dt);
                unrelated.Tick(dt * 0.7f);
                var actual = second.Tick(dt);
                Assert.AreEqual(expected.Offset, actual.Offset);
                Assert.AreEqual(expected.Rotation, actual.Rotation);
                Assert.AreEqual(first.Phase, second.Phase);
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaLeavesPoseAndScheduleUnchanged(float dt)
        {
            var bob = new BirdBob(713);
            var control = new BirdBob(713);
            float advance = bob.Duration + 0.1f;
            var before = bob.Tick(advance);
            control.Tick(advance);
            for (int i = 0; i < 100; i++)
            {
                var pose = bob.Tick(dt);
                Assert.AreEqual(before.Offset, pose.Offset);
                Assert.AreEqual(before.Rotation, pose.Rotation);
            }
            var expected = control.Tick(Dt);
            var actual = bob.Tick(Dt);
            Assert.AreEqual(expected.Offset, actual.Offset);
            Assert.AreEqual(expected.Rotation, actual.Rotation);
        }

        [Test]
        public void TenThousandTicksAllocateZeroBytes()
        {
            var bob = new BirdBob(713);
            for (int i = 0; i < 1000; i++) bob.Tick(Dt);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) bob.Tick(Dt);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }
    }
}
