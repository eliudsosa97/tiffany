using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class StickResponseTests
    {
        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(StickResponse.Deadzone)]
        public void DeadzoneIgnoresDrift(float magnitude)
        {
            Assert.AreEqual(Vector2.zero, StickResponse.Shape(Vector2.up * magnitude));
        }

        [TestCase(0.325f, 2f)]
        [TestCase(0.5f, 4f)]
        [TestCase(1f, 5.5f)]
        public void PhysicalStickPassesThroughPartialWalkWalkAndRun(float raw, float expected)
        {
            float amount = StickResponse.Shape(Vector2.up * raw).magnitude;
            Assert.AreEqual(expected, PlayerMotion.TargetSpeed(amount, Gait.Run), 1e-4f);
        }

        [Test]
        public void CurveIsContinuousMonotonicAndHasAFineStart()
        {
            float previous = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float current = StickResponse.Shape(Vector2.up * (i / 1000f)).magnitude;
                Assert.That(current - previous, Is.InRange(0f, 0.003f));
                previous = current;
            }
            float start = StickResponse.Shape(Vector2.up * 0.151f).magnitude;
            Assert.Greater(start, 0f);
            Assert.Less(start, 0.0001f);
            Assert.AreEqual(1f, previous);
        }

        [Test]
        public void RadialCurvePreservesDirectionAndClampsDiagonals()
        {
            var diagonal = new Vector2(0.3f, 0.4f);
            Assert.Less(Vector2.Distance(diagonal, StickResponse.Shape(diagonal)), 1e-5f);
            var full = StickResponse.Shape(Vector2.one);
            Assert.AreEqual(1f, full.magnitude, 1e-5f);
            Assert.AreEqual(full.x, full.y);
        }
    }
}
