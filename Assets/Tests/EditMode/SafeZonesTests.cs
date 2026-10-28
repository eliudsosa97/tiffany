using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class SafeZonesTests
    {
        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SafeZones.Clear();
        }

        [Test]
        public void EmptyRegistryContainsNothingAndLeavesPointUnchanged()
        {
            Vector3 point = new Vector3(1f, 20f, 3f);
            Assert.AreEqual(0, SafeZones.Count);
            Assert.IsFalse(SafeZones.Contains(point));
            Assert.AreEqual(point, SafeZones.PushOut(point, 1f));
        }

        [Test]
        public void HandlesIncreaseAcrossRemovalAndClear()
        {
            int first = SafeZones.Add(Vector3.zero, 2f);
            int second = SafeZones.Add(Vector3.right * 10f, 2f);
            Assert.GreaterOrEqual(first, 1);
            Assert.AreEqual(first + 1, second);
            Assert.AreEqual(2, SafeZones.Count);

            SafeZones.Remove(second);
            int third = SafeZones.Add(Vector3.forward * 10f, 2f);
            Assert.AreEqual(second + 1, third);
            SafeZones.Clear();
            Assert.AreEqual(0, SafeZones.Count);
            Assert.IsFalse(SafeZones.Contains(Vector3.zero));
            Assert.IsFalse(SafeZones.Contains(Vector3.forward * 10f));
            Assert.AreEqual(third + 1, SafeZones.Add(Vector3.one, 2f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AddRejectsNonPositiveOrNaNRadius(float radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SafeZones.Add(Vector3.zero, radius));
            Assert.AreEqual(0, SafeZones.Count);
        }

        [TestCase(0f, 0f, true)]
        [TestCase(0f, 4.9f, true)]
        [TestCase(3f, 4f, false)]
        [TestCase(-3f, -4f, false)]
        [TestCase(0f, 5.1f, false)]
        public void ContainsUsesHorizontalDistanceAndExclusiveBoundary(float x, float z, bool expected)
        {
            Vector3 center = new Vector3(10f, -20f, -8f);
            SafeZones.Add(center, 5f);

            Assert.AreEqual(expected, SafeZones.Contains(new Vector3(center.x + x, 30f, center.z + z)));
        }

        [Test]
        public void RemoveOnlyDeletesMatchingZoneAndIgnoresUnknownHandles()
        {
            int first = SafeZones.Add(Vector3.zero, 2f);
            int second = SafeZones.Add(Vector3.right * 10f, 2f);
            SafeZones.Remove(0);
            Assert.AreEqual(2, SafeZones.Count);

            SafeZones.Remove(first);
            SafeZones.Remove(first);
            Assert.AreEqual(1, SafeZones.Count);
            Assert.IsFalse(SafeZones.Contains(Vector3.zero));
            Assert.IsTrue(SafeZones.Contains(Vector3.right * 10f));
            SafeZones.Remove(second);
            Assert.AreEqual(0, SafeZones.Count);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void PushOutMovesToBoundaryPlusMarginAndPreservesHeight(float margin)
        {
            Vector3 center = new Vector3(10f, -20f, -8f);
            SafeZones.Add(center, 5f);
            Vector3 point = new Vector3(10.6f, 30f, -7.2f);

            Vector3 result = SafeZones.PushOut(point, margin);
            Assert.AreEqual(center.x + 0.6f * (5f + margin + 1e-3f), result.x, 1e-4f);
            Assert.AreEqual(center.z + 0.8f * (5f + margin + 1e-3f), result.z, 1e-4f);
            Assert.AreEqual(point.y, result.y);
        }

        [Test]
        public void PushOutAlsoMovesPointsInsideMarginButOutsideZone()
        {
            SafeZones.Add(Vector3.zero, 2f);
            Vector3 point = new Vector3(2.5f, 17f, 0f);
            Assert.IsFalse(SafeZones.Contains(point));

            Vector3 result = SafeZones.PushOut(point, 1f);
            Assert.AreEqual(3.001f, result.x, 1e-4f);
            Assert.AreEqual(point.y, result.y);
            Assert.AreEqual(0f, result.z);
        }

        [Test]
        public void PushOutAtHorizontalCenterUsesForwardAndPreservesHeight()
        {
            SafeZones.Add(new Vector3(10f, -20f, -8f), 2f);

            Vector3 result = SafeZones.PushOut(new Vector3(10f, 30f, -8f), 1f);
            Assert.AreEqual(10f, result.x);
            Assert.AreEqual(30f, result.y);
            Assert.AreEqual(-4.999f, result.z, 1e-4f);
        }

        [TestCase(0.25f)]
        [TestCase(-0.25f)]
        public void PushOutFindsNearestExitAcrossOverlappingZones(float z)
        {
            SafeZones.Add(new Vector3(0f, -20f, 0f), 2f);
            SafeZones.Add(new Vector3(3f, 20f, 0f), 2f);
            Vector3 point = new Vector3(1.5f, 17f, z);

            Vector3 result = SafeZones.PushOut(point, 0.5f);
            Assert.IsFalse(SafeZones.Contains(result));
            Assert.AreEqual(1.5f, result.x, 1e-4f);
            Assert.AreEqual(Mathf.Sign(z) * 2.00125f, result.z, 1e-4f);
            Assert.AreEqual(point.y, result.y);
            result.y = 0f;
            Assert.GreaterOrEqual(result.magnitude, 2.5f - 1e-4f);
            Assert.GreaterOrEqual((result - Vector3.right * 3f).magnitude, 2.5f - 1e-4f);
        }

        [TestCase(3f)]
        [TestCase(4f)]
        public void PushOutLeavesPointsAtOrBeyondMarginUnchanged(float distance)
        {
            SafeZones.Add(Vector3.zero, 2f);
            Vector3 point = new Vector3(0f, 17f, distance);

            Assert.AreEqual(point, SafeZones.PushOut(point, 1f));
        }

        [Test]
        public void PushOutEscapesOverlappingTriangleAcrossGrid()
        {
            Vector3[] centers = { Vector3.zero, Vector3.right * 3f, new Vector3(1.5f, 0f, 2.6f) };
            for (int i = 0; i < centers.Length; i++)
            {
                SafeZones.Add(centers[i], 2f);
            }

            Vector3 point = new Vector3(1.5f, 0f, 0.87f);
            Assert.IsTrue(SafeZones.TryPushOut(point, 0.5f, out Vector3 result));
            Assert.AreEqual(result, SafeZones.PushOut(point, 0.5f));
            AssertOutsideZones(result, centers, 2.5f - 1e-3f);

            for (int x = -12; x <= 24; x++)
            {
                for (int z = -12; z <= 24; z++)
                {
                    point = new Vector3(x * 0.25f, 0f, z * 0.25f);
                    result = SafeZones.PushOut(point, 0.5f);
                    AssertOutsideZones(result, centers, 2.5f - 1e-3f);
                    Assert.AreEqual(point.y, result.y);
                }
            }
        }

        [Test]
        public void PushOutAtCenterFindsNearestExitWhenForwardIsBlocked()
        {
            Vector3[] centers = { Vector3.zero, Vector3.forward * 3f };
            for (int i = 0; i < centers.Length; i++)
            {
                SafeZones.Add(centers[i], 2f);
            }

            Vector3 result = SafeZones.PushOut(Vector3.zero, 0.5f);

            AssertOutsideZones(result, centers, 2.5f - 1e-3f);
            Assert.AreEqual(2.5f, result.magnitude, 2e-3f);
        }

        [Test]
        public void PushOutWithZeroMarginStaysOutsideZone()
        {
            SafeZones.Add(Vector3.zero, 2f);
            Vector3 point = new Vector3(-1.5f, 17f, -1.1f);

            Vector3 result = SafeZones.PushOut(point, 0f);

            Assert.IsFalse(SafeZones.Contains(result));
            Assert.AreEqual(point.y, result.y);
            result.y = 0f;
            Assert.GreaterOrEqual(result.magnitude, 2.001f - 1e-4f);
        }

        [TestCase(-0.5f)]
        [TestCase(-10f)]
        public void PushOutTreatsNegativeMarginAsZero(float margin)
        {
            SafeZones.Add(Vector3.zero, 2f);
            Vector3 point = new Vector3(1f, 17f, 0f);

            Vector3 result = SafeZones.PushOut(point, margin);

            Assert.AreEqual(SafeZones.PushOut(point, 0f), result);
            Assert.IsFalse(SafeZones.Contains(result));
        }

        [TestCase(3f)]
        [TestCase(4f)]
        public void TryPushOutLeavesPointsAtOrBeyondMarginUnchanged(float distance)
        {
            SafeZones.Add(Vector3.zero, 2f);
            Vector3 point = new Vector3(0f, 17f, distance);

            Assert.IsTrue(SafeZones.TryPushOut(point, 1f, out Vector3 result));
            Assert.AreEqual(point, result);
        }

        private static void AssertOutsideZones(Vector3 result, Vector3[] centers, float minimumDistance)
        {
            Assert.IsFalse(SafeZones.Contains(result));
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 offset = result - centers[i];
                offset.y = 0f;
                Assert.GreaterOrEqual(offset.magnitude, minimumDistance);
            }
        }
    }
}
