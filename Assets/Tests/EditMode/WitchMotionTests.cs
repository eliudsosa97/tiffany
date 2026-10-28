using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchMotionTests
    {
        [TestCase(1f, 6f)]
        [TestCase(3f, 10f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        public void FlyMovesAtSpeedWithoutOvershootingAndIgnoresInvalidTime(float dt, float expectedX)
        {
            Vector3 result = WitchMotion.Fly(new Vector3(0f, 14f, 0f), new Vector3(10f, 14f, 0f), 6f, dt);

            Assert.AreEqual(new Vector3(expectedX, 14f, 0f), result);
        }

        [TestCase(-1f, 2f, 0f)]
        [TestCase(0f, 2f, 0f)]
        [TestCase(0.5f, 2f, 0.15625f)]
        [TestCase(1f, 2f, 0.5f)]
        [TestCase(2f, 2f, 1f)]
        [TestCase(3f, 2f, 1f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, -1f, 1f)]
        public void VerticalSmoothsAndClampsProgress(float elapsed, float duration, float fraction)
        {
            var from = new Vector3(2f, 14f, -6f);
            var to = new Vector3(8f, 0f, 4f);

            Assert.AreEqual(Vector3.Lerp(from, to, fraction), WitchMotion.Vertical(from, to, elapsed, duration));
        }

        [TestCase(1f, float.PositiveInfinity, 20f, 6f)]
        [TestCase(1f, 2f, 20f, 2f)]
        [TestCase(1f, 10f, 3f, 3f)]
        [TestCase(1f, -2f, 20f, 0f)]
        [TestCase(-1f, 10f, 20f, 0f)]
        [TestCase(float.NaN, 10f, 20f, 0f)]
        public void SprintStepLimitsHorizontalTravelAndPreservesHeight(float dt, float freeDistance,
            float endX, float expectedX)
        {
            Vector3 result = WitchMotion.SprintStep(new Vector3(0f, 3f, 0f), new Vector3(endX, 100f, 0f),
                6f, dt, freeDistance);

            Assert.AreEqual(new Vector3(expectedX, 3f, 0f), result);
        }

        [Test]
        public void SprintStepMovesDiagonallyAtTheSameSpeed()
        {
            Vector3 result = WitchMotion.SprintStep(new Vector3(0f, 3f, 0f), new Vector3(3f, -10f, 4f),
                5f, 0.5f, float.PositiveInfinity);

            Assert.AreEqual(new Vector3(1.5f, 3f, 2f), result);
        }

        [Test]
        public void CruiseHeightChoosesNearestPointHorizontallyInsteadOfInThreeDimensions()
        {
            var route = new[] { new Vector3(5f, 0f, 0f), new Vector3(0f, 100f, 1f) };

            Assert.AreEqual(100f, WitchMotion.CruiseHeight(route, Vector3.zero, 12f));
        }

        [Test]
        public void CruiseHeightFallsBackWithoutARoute()
        {
            Assert.AreEqual(12f, WitchMotion.CruiseHeight(null, Vector3.zero, 12f));
            Assert.AreEqual(12f, WitchMotion.CruiseHeight(Array.Empty<Vector3>(), Vector3.zero, 12f));
        }

        [TestCase(0f, 100f, false)]
        [TestCase(0.5f, -100f, false)]
        [TestCase(0.51f, 100f, true)]
        public void ShouldRepathUsesStrictHorizontalThreshold(float x, float y, bool expected)
        {
            Assert.AreEqual(expected, WitchMotion.ShouldRepath(Vector3.zero, new Vector3(x, y, 0f), 0.5f));
        }
    }
}
