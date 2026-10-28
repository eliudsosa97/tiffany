using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class RouteLayoutTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void RouteLoopsRightAlongTheLowerRowAndLeftBackToTheHouse()
        {
            Assert.AreEqual(8, RouteLayout.Count);
            Assert.AreEqual(36f, RouteLayout.ClearingSize);
            Assert.AreEqual(5f, RouteLayout.PathWidth);
            Assert.AreEqual(6f, RouteLayout.MaxSlopeDegrees);
            Vector3 cemetery = RouteLayout.Clearing(0);
            for (int i = 1; i <= 3; i++)
            {
                Assert.Greater(RouteLayout.Clearing(i).x, RouteLayout.Clearing(i - 1).x);
                Assert.AreEqual(cemetery.z, RouteLayout.Clearing(i).z, Tolerance);
            }
            Assert.Greater(RouteLayout.Clearing(4).x, RouteLayout.Clearing(3).x);
            Assert.Greater(RouteLayout.Clearing(4).z, RouteLayout.Clearing(3).z);
            for (int i = 5; i < RouteLayout.Count; i++)
            {
                Assert.Less(RouteLayout.Clearing(i).x, RouteLayout.Clearing(i - 1).x);
                Assert.Greater(RouteLayout.Clearing(i).z, cemetery.z);
            }
            Assert.AreEqual(cemetery.x, RouteLayout.Clearing(7).x, Tolerance);
        }

        [Test]
        public void HutIsTheHighestClearingAndTheReturnDescends()
        {
            float summit = RouteLayout.Clearing(4).y;
            Assert.That(summit - RouteLayout.Clearing(0).y, Is.InRange(4f, 6f));
            for (int i = 0; i < RouteLayout.Count; i++)
                if (i != 4) Assert.Less(RouteLayout.Clearing(i).y, summit);
            for (int i = 5; i < RouteLayout.Count; i++)
                Assert.Less(RouteLayout.Clearing(i).y, RouteLayout.Clearing(i - 1).y);
        }

        [Test]
        public void ClearingsLeaveAtLeastFourteenMetersOfForestBetweenTheirSquareEdges()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
                for (int j = i + 1; j < RouteLayout.Count; j++)
                {
                    Vector3 delta = RouteLayout.Clearing(j) - RouteLayout.Clearing(i);
                    float x = Mathf.Max(0f, Mathf.Abs(delta.x) - RouteLayout.ClearingSize);
                    float z = Mathf.Max(0f, Mathf.Abs(delta.z) - RouteLayout.ClearingSize);
                    Assert.GreaterOrEqual(Mathf.Sqrt(x * x + z * z), 14f, $"Claros {i} y {j}");
                }
        }

        [Test]
        public void ClearingsAndPathWidthsFitInsideTheRouteFootprint()
        {
            var size = new Vector3(RouteLayout.ClearingSize, 0f, RouteLayout.ClearingSize);
            var bounds = new Bounds(RouteLayout.Clearing(0), size);
            for (int i = 1; i < RouteLayout.Count; i++)
                bounds.Encapsulate(new Bounds(RouteLayout.Clearing(i), size));
            for (int from = 0; from < RouteLayout.Count - 1; from++)
                foreach (Vector3 point in RouteLayout.Path(from))
                    bounds.Encapsulate(new Bounds(point, new Vector3(RouteLayout.PathWidth, 0f, RouteLayout.PathWidth)));
            Assert.LessOrEqual(bounds.size.x, 260f);
            Assert.LessOrEqual(bounds.size.z, 140f);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void PathJoinsItsClearingEdgesAtGroundHeightAndMeanders(int from)
        {
            var path = RouteLayout.Path(from);
            Assert.GreaterOrEqual(path.Length, 3);
            AssertOnEdge(path[0], RouteLayout.Clearing(from));
            AssertOnEdge(path[path.Length - 1], RouteLayout.Clearing(from + 1));
            float deviation = 0f;
            for (int i = 1; i < path.Length - 1; i++)
                deviation = Mathf.Max(deviation, PointDistance(Xz(path[i]), Xz(path[0]), Xz(path[path.Length - 1])));
            Assert.GreaterOrEqual(deviation, 2f);
        }

        [Test]
        public void SlopesNeverExceedSixDegreesAndAtLeastThreePathsReachThreeDegrees()
        {
            int slopingPaths = 0;
            for (int from = 0; from < RouteLayout.Count - 1; from++)
            {
                var path = RouteLayout.Path(from);
                float maximum = 0f;
                for (int i = 1; i < path.Length; i++)
                {
                    Assert.Greater(Vector2.Distance(Xz(path[i - 1]), Xz(path[i])), Tolerance);
                    float slope = RouteLayout.Slope(path[i - 1], path[i]);
                    Assert.LessOrEqual(slope, RouteLayout.MaxSlopeDegrees + Tolerance, $"Sendero {from}, tramo {i}");
                    maximum = Mathf.Max(maximum, slope);
                }
                if (maximum >= 3f) slopingPaths++;
            }
            Assert.GreaterOrEqual(slopingPaths, 3);
        }

        [Test]
        public void RiverIsOneStraightFlatSegmentOfAtLeastTwentyMetersOnTheHutToLakePath()
        {
            RouteLayout.RiverSpan(out var start, out var end);
            Assert.GreaterOrEqual(Vector2.Distance(Xz(start), Xz(end)), 20f);
            Assert.AreEqual(start.y, end.y, Tolerance);
            var path = RouteLayout.Path(4);
            bool found = false;
            for (int i = 1; i < path.Length; i++)
                if (path[i - 1] == start && path[i] == end) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void PathCorridorsDoNotCrossEachOtherOrUnrelatedClearings()
        {
            float half = (RouteLayout.ClearingSize + RouteLayout.PathWidth) * 0.5f;
            for (int from = 0; from < RouteLayout.Count - 1; from++)
            {
                var path = RouteLayout.Path(from);
                for (int i = 1; i < path.Length; i++)
                {
                    Vector2 a = Xz(path[i - 1]), b = Xz(path[i]);
                    for (int clearing = 0; clearing < RouteLayout.Count; clearing++)
                        if (clearing != from && clearing != from + 1)
                            Assert.IsFalse(IntersectsSquare(a, b, Xz(RouteLayout.Clearing(clearing)), half),
                                $"Sendero {from}, tramo {i}, claro {clearing}");
                    for (int other = from; other < RouteLayout.Count - 1; other++)
                    {
                        var next = RouteLayout.Path(other);
                        for (int j = other == from ? i + 2 : 1; j < next.Length; j++)
                            Assert.GreaterOrEqual(SegmentDistance(a, b, Xz(next[j - 1]), Xz(next[j])), RouteLayout.PathWidth,
                                $"Senderos {from}/{other}, tramos {i}/{j}");
                    }
                }
            }
        }

        [Test]
        public void GateCrossesTheGapBetweenHouseAndCemeteryOutsideBothClearings()
        {
            RouteLayout.Gate(out var position, out float yaw, out float width);
            Vector3 cemetery = RouteLayout.Clearing(0), house = RouteLayout.Clearing(7);
            Vector2 direction = Xz(house - cemetery);
            float fraction = Vector2.Dot(Xz(position - cemetery), direction) / direction.sqrMagnitude;
            Assert.That(fraction, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.AreEqual(0f, PointDistance(Xz(position), Xz(cemetery), Xz(house)), Tolerance);
            Assert.AreEqual(Mathf.Lerp(cemetery.y, house.y, fraction), position.y, Tolerance);
            Assert.GreaterOrEqual(width, RouteLayout.PathWidth + 2f);
            Vector2 axis = Xz(Quaternion.Euler(0f, yaw, 0f) * Vector3.right);
            Assert.AreEqual(0f, Vector2.Dot(axis, direction.normalized), Tolerance);
            Vector2 a = Xz(position) - axis * width * 0.5f, b = Xz(position) + axis * width * 0.5f;
            Assert.AreEqual(0f, SegmentDistance(a, b, Xz(cemetery), Xz(house)), Tolerance);
            Assert.IsFalse(IntersectsSquare(a, b, Xz(cemetery), RouteLayout.ClearingSize * 0.5f));
            Assert.IsFalse(IntersectsSquare(a, b, Xz(house), RouteLayout.ClearingSize * 0.5f));
        }

        [Test]
        public void PathCopiesCannotChangeTheStoredLayout()
        {
            for (int from = 0; from < RouteLayout.Count - 1; from++)
            {
                var original = RouteLayout.Path(from);
                var copy = RouteLayout.Path(from);
                Assert.AreNotSame(original, copy);
                copy[1] = Vector3.positiveInfinity;
                CollectionAssert.AreEqual(original, RouteLayout.Path(from));
            }
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void InvalidClearingIndicesAreRejected(int index) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => RouteLayout.Clearing(index));

        [TestCase(-1)]
        [TestCase(7)]
        [TestCase(8)]
        public void InvalidPathsIncludingHouseToCemeteryAreRejected(int from) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => RouteLayout.Path(from));

        [TestCase(3f, 0f, 4f, 0f)]
        [TestCase(0f, 1f, 1f, 45f)]
        [TestCase(3f, -5f, 4f, 45f)]
        [TestCase(0f, 2f, 0f, 90f)]
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(3f, 0.3f, 4f, 3.43363f)]
        public void SlopeUsesHorizontalDistanceAndIsIndependentOfTravelDirection(float x, float y, float z, float expected)
        {
            var start = new Vector3(7f, 2f, -3f);
            Vector3 end = start + new Vector3(x, y, z);
            Assert.AreEqual(expected, RouteLayout.Slope(start, end), Tolerance);
            Assert.AreEqual(expected, RouteLayout.Slope(end, start), Tolerance);
        }

        static void AssertOnEdge(Vector3 point, Vector3 center)
        {
            Vector3 delta = point - center;
            Assert.AreEqual(RouteLayout.ClearingSize * 0.5f, Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.z)), 0.5f);
            Assert.AreEqual(center.y, point.y, Tolerance);
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float PointDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float fraction = delta.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * fraction);
        }

        static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float denominator = Cross(b - a, d - c);
            if (Mathf.Abs(denominator) > Tolerance)
            {
                float t = Cross(c - a, d - c) / denominator;
                float u = Cross(c - a, b - a) / denominator;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return 0f;
            }
            return Mathf.Min(Mathf.Min(PointDistance(a, c, d), PointDistance(b, c, d)),
                Mathf.Min(PointDistance(c, a, b), PointDistance(d, a, b)));
        }

        static bool IntersectsSquare(Vector2 a, Vector2 b, Vector2 center, float half)
        {
            Vector2 delta = b - a;
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) <= Tolerance)
                {
                    if (Mathf.Abs(a[axis] - center[axis]) > half) return false;
                    continue;
                }
                float first = (center[axis] - half - a[axis]) / delta[axis];
                float last = (center[axis] + half - a[axis]) / delta[axis];
                enter = Mathf.Max(enter, Mathf.Min(first, last));
                exit = Mathf.Min(exit, Mathf.Max(first, last));
                if (enter > exit) return false;
            }
            return true;
        }
    }
}
