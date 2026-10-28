using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class RouteLayoutTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void MapKeepsTheCemeteryNortheastPlantEastStableSouthAndLakeWest()
        {
            Assert.AreEqual(8, RouteLayout.Count);
            Assert.AreEqual(36f, RouteLayout.ClearingSize);
            Assert.AreEqual(5f, RouteLayout.PathWidth);
            Assert.AreEqual(6f, RouteLayout.MaxSlopeDegrees);
            Vector3 cemetery = RouteLayout.Clearing(0);
            Vector3 plant = RouteLayout.Clearing(1), tracks = RouteLayout.Clearing(2);
            Vector3 stable = RouteLayout.Clearing(3), hut = RouteLayout.Clearing(4), lake = RouteLayout.Clearing(5);
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 clearing = RouteLayout.Clearing(i);
                Assert.GreaterOrEqual(cemetery.x + cemetery.z, clearing.x + clearing.z);
                Assert.GreaterOrEqual(plant.x, clearing.x);
                if (i != 3) Assert.Less(stable.z, clearing.z);
                if (i != 5) Assert.Less(lake.x, clearing.x);
            }
            Assert.Less(plant.z, cemetery.z);
            Assert.Less(tracks.z, plant.z);
            Assert.Greater(tracks.x, stable.x);
            Assert.Less(lake.x, hut.x);
            for (int i = 1; i <= 5; i++)
            {
                Assert.Greater(RouteLayout.Clearing(6).z, RouteLayout.Clearing(i).z);
                Assert.Greater(RouteLayout.Clearing(7).z, RouteLayout.Clearing(i).z);
            }
            var perimeter = new[] { 0, 1, 2, 3, 5, 6, 7 };
            bool inside = false;
            for (int i = 0; i < perimeter.Length; i++)
            {
                Vector3 a = RouteLayout.Clearing(perimeter[i]);
                Vector3 b = RouteLayout.Clearing(perimeter[(i + 1) % perimeter.Length]);
                if ((a.z > hut.z) != (b.z > hut.z) && hut.x < (b.x - a.x) * (hut.z - a.z) / (b.z - a.z) + a.x)
                    inside = !inside;
            }
            Assert.IsTrue(inside, "La choza queda dentro del contorno exterior.");
        }

        [Test]
        public void HutIsTheHighestClearingAndTheReturnDescends()
        {
            float summit = RouteLayout.Clearing(4).y;
            Assert.AreEqual(0f, RouteLayout.Clearing(0).y, Tolerance);
            Assert.AreEqual(0f, RouteLayout.Clearing(7).y, Tolerance);
            Assert.That(summit - RouteLayout.Clearing(0).y, Is.InRange(4f, 6f));
            for (int i = 0; i < RouteLayout.Count; i++)
                if (i != 4) Assert.Less(RouteLayout.Clearing(i).y, summit);
            for (int i = 1; i <= 4; i++)
                Assert.Greater(RouteLayout.Clearing(i).y, RouteLayout.Clearing(i - 1).y);
            for (int i = 5; i < RouteLayout.Count; i++)
                Assert.Less(RouteLayout.Clearing(i).y, RouteLayout.Clearing(i - 1).y);
        }

        [Test]
        public void ClearingSquaresDoNotOverlapAndNonConsecutiveCentersStayFiftyTwoMetersApart()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
                for (int j = i + 1; j < RouteLayout.Count; j++)
                {
                    Vector3 delta = RouteLayout.Clearing(j) - RouteLayout.Clearing(i);
                    float x = Mathf.Max(0f, Mathf.Abs(delta.x) - RouteLayout.ClearingSize);
                    float z = Mathf.Max(0f, Mathf.Abs(delta.z) - RouteLayout.ClearingSize);
                    Assert.Greater(Mathf.Sqrt(x * x + z * z), 0f, $"Claros {i} y {j}");
                    if (j != i + 1)
                        Assert.GreaterOrEqual(Xz(delta).magnitude, 52f, $"Claros {i} y {j}");
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
            Assert.LessOrEqual(bounds.size.x, 220f);
            Assert.LessOrEqual(bounds.size.z, 220f);
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
            AssertPerpendicular(path[0], path[1], RouteLayout.Clearing(from));
            AssertPerpendicular(path[path.Length - 1], path[path.Length - 2], RouteLayout.Clearing(from + 1));
            float length = 0f;
            for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            Assert.That(length, Is.InRange(16f, 45f));
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
                    if (slope > Tolerance) Assert.GreaterOrEqual(slope, 3f - Tolerance, $"Sendero {from}, tramo {i}");
                    float rise = path[i].y - path[i - 1].y;
                    float totalRise = RouteLayout.Clearing(from + 1).y - RouteLayout.Clearing(from).y;
                    Assert.GreaterOrEqual(rise * totalRise, -Tolerance);
                    maximum = Mathf.Max(maximum, slope);
                }
                if (maximum >= 3f) slopingPaths++;
            }
            Assert.GreaterOrEqual(slopingPaths, 3);
        }

        [Test]
        public void RiverIsAFlatNorthboundSegmentOfAtLeastTwelveMetersBetweenLakeAndRoad()
        {
            RouteLayout.RiverSpan(out var start, out var end);
            Assert.GreaterOrEqual(Vector2.Distance(Xz(start), Xz(end)), 12f);
            Assert.AreEqual(start.y, end.y, Tolerance);
            Assert.LessOrEqual(Vector2.Angle(Xz(end - start), Vector2.up), 20f);
            var path = RouteLayout.Path(5);
            bool found = false;
            for (int i = 1; i < path.Length; i++)
                if (path[i - 1] == start && path[i] == end) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void PathCorridorsDoNotCrossEachOtherOrUnrelatedClearings()
        {
            float half = (RouteLayout.ClearingSize + RouteLayout.PathWidth) * 0.5f + 4f;
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
                            Assert.GreaterOrEqual(SegmentDistance(a, b, Xz(next[j - 1]), Xz(next[j])), other == from ? 0.01f : RouteLayout.PathWidth,
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
            Assert.GreaterOrEqual(width + Tolerance, RouteLayout.ClearingSize * (Mathf.Abs(axis.x) + Mathf.Abs(axis.y)));
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

        [Test]
        public void OpeningsOnlyExposeTheIncomingAndOutgoingPaths()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var openings = RouteLayout.Openings(i);
                Assert.AreEqual(i == 0 || i == RouteLayout.Count - 1 ? 1 : 2, openings.Length);
                if (i > 0)
                {
                    var previous = RouteLayout.Path(i - 1);
                    CollectionAssert.Contains(openings, previous[previous.Length - 1]);
                }
                if (i < RouteLayout.Count - 1) CollectionAssert.Contains(openings, RouteLayout.Path(i)[0]);
                var original = RouteLayout.Openings(i);
                openings[0] = Vector3.positiveInfinity;
                CollectionAssert.AreEqual(original, RouteLayout.Openings(i));
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
            Assert.AreEqual(RouteLayout.ClearingSize * 0.5f, Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.z)), Tolerance);
            Assert.LessOrEqual(Mathf.Min(Mathf.Abs(delta.x), Mathf.Abs(delta.z)), (RouteLayout.ClearingSize - RouteLayout.PathWidth) * 0.5f);
            Assert.AreEqual(center.y, point.y, Tolerance);
        }

        static void AssertPerpendicular(Vector3 end, Vector3 adjacent, Vector3 clearing)
        {
            Vector3 local = end - clearing;
            Vector3 normal = Mathf.Abs(local.x) > Mathf.Abs(local.z)
                ? Vector3.right * Mathf.Sign(local.x) : Vector3.forward * Mathf.Sign(local.z);
            Vector3 outward = Vector3.ProjectOnPlane(adjacent - end, Vector3.up);
            Assert.GreaterOrEqual(outward.magnitude, 4f);
            Assert.AreEqual(1f, Vector3.Dot(normal, outward.normalized), Tolerance);
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
