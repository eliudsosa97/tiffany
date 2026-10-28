using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PoltergeistTargetingTests
    {
        [TestCase(0f, 0f, 5f, true)]
        [TestCase(0f, 0f, 10.1f, false)]
        [TestCase(5f, 0f, 1f, false)]
        [TestCase(0f, 5f, 1f, false)]
        [TestCase(0f, 10.1f, 1f, false)]
        [TestCase(0f, 0f, 0f, true)]
        public void SpotUsesThreeDimensionalRangeAndAngle(float x, float y, float z, bool expected)
        {
            Vector3 origin = new Vector3(2f, 3f, 4f);

            Assert.AreEqual(expected, PoltergeistTargeting.InSpot(origin, Vector3.forward,
                10f, 84f, origin + new Vector3(x, y, z)));
        }

        [Test]
        public void SpotIncludesExactRangeAndAngleBoundary()
        {
            Vector3 point = Quaternion.Euler(0f, 42f, 0f) * Vector3.forward * 10f;

            Assert.IsTrue(PoltergeistTargeting.InSpot(Vector3.zero, Vector3.forward, 10f, 84f, point));
        }

        [Test]
        public void SpotRejectsZeroForwardAndNegativeRange()
        {
            Assert.IsFalse(PoltergeistTargeting.InSpot(Vector3.zero, Vector3.zero, 10f, 84f, Vector3.forward));
            Assert.IsFalse(PoltergeistTargeting.InSpot(Vector3.zero, Vector3.zero, 10f, 84f, Vector3.zero));
            Assert.IsFalse(PoltergeistTargeting.InSpot(Vector3.zero, Vector3.forward, -1f, 84f, Vector3.zero));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 90f)]
        [TestCase(0f, -1f, 180f)]
        public void AimAngleIgnoresHeight(float x, float z, float expected)
        {
            Vector3 from = new Vector3(2f, 3f, 4f);
            Vector3 target = from + new Vector3(x, 100f, z);

            Assert.AreEqual(expected, PoltergeistTargeting.AimAngle(from,
                new Vector3(0f, -8f, 1f), target), 1e-4f);
        }

        [Test]
        public void AimAngleIsZeroWithoutHorizontalDisplacement()
        {
            Assert.AreEqual(0f, PoltergeistTargeting.AimAngle(Vector3.zero, Vector3.forward, Vector3.up));
        }

        [Test]
        public void AimAngleIsOneHundredEightyWithoutHorizontalForward()
        {
            Assert.AreEqual(180f, PoltergeistTargeting.AimAngle(Vector3.zero, Vector3.up, Vector3.forward));
        }

        [TestCase(10f, 8f, 20f, 2f, true)]
        [TestCase(20f, 2f, 10f, 8f, false)]
        [TestCase(10f, 2f, 10f, 8f, true)]
        [TestCase(10f, 8f, 10f, 2f, false)]
        [TestCase(10f, 2f, 10f, 2f, false)]
        [TestCase(10.000001f, 2f, 10f, 8f, true)]
        [TestCase(9.999999f, 8f, 10f, 2f, false)]
        public void BetterAimWinsThenNearestDistanceBreaksApproximateTie(float angle, float distance,
            float bestAngle, float bestDistance, bool expected)
        {
            Assert.AreEqual(expected, PoltergeistTargeting.IsBetter(angle, distance, bestAngle, bestDistance));
        }
    }
}
