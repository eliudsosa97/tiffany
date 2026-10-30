using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class TrailBirdLayoutTests
    {
        [TestCase(0)]
        [TestCase(713)]
        [TestCase(-17)]
        public void StraightTrailKeepsFifteenToTwentyFiveMetersAndAlternatesSides(int seed)
        {
            var path = new[] { new Vector3(180f, 0f, -80f), new Vector3(180f, 0f, 40f) };
            var points = TrailBirdLayout.Points(path, new RouteTerrain(), seed);
            Assert.GreaterOrEqual(points.Count, 6);
            for (int i = 1; i < points.Count; i++)
            {
                Assert.That(points[i].Distance - points[i - 1].Distance, Is.InRange(15f, 25f));
                Assert.That(Vector3.Distance(points[i].Position, points[i - 1].Position), Is.InRange(15f, 25f));
                Assert.Less((points[i].Position.x - 180f) * (points[i - 1].Position.x - 180f), 0f);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void RouteHasEveryOutsideBendDryPerchesAndAnUnbrokenChain(int index)
        {
            var terrain = new RouteTerrain();
            var path = RouteLayout.Path(index);
            var points = TrailBirdLayout.Points(path, terrain, 713 + index);
            Assert.IsNotEmpty(points);
            Assert.LessOrEqual(points[0].Distance, 25f);
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                float axisDistance = float.PositiveInfinity;
                for (int segment = 1; segment < path.Length; segment++)
                    axisDistance = Mathf.Min(axisDistance, RouteTerrain.SegmentDistance(Xz(point.Position), Xz(path[segment - 1]), Xz(path[segment])));
                Assert.That(axisDistance, Is.InRange(3f, 3.5f));
                Assert.Greater(axisDistance, RouteLayout.PathWidth * 0.5f);
                Assert.IsFalse(terrain.IsWater(point.Position.x, point.Position.z));
                RouteLayout.RiverSpan(out var start, out var end);
                float riverAlong = Vector2.Dot(Xz(point.Position - start), Xz(end - start)) / Xz(end - start).sqrMagnitude;
                Assert.IsFalse(riverAlong >= 0f && riverAlong <= 1f &&
                    RouteTerrain.SegmentDistance(Xz(point.Position), Xz(start), Xz(end)) <= 6f);
                Assert.AreEqual(terrain.Height(point.Position.x, point.Position.z), point.Position.y);
                if (i > 0)
                {
                    Assert.Greater(point.Distance, points[i - 1].Distance);
                    Assert.LessOrEqual(Vector3.Distance(point.Position, points[i - 1].Position), 25f);
                    if (point.BendIndex < 0 && points[i - 1].BendIndex < 0)
                        Assert.That(point.Distance - points[i - 1].Distance, Is.InRange(15f, 25f));
                }
            }
            float distance = 0f;
            for (int vertex = 1; vertex < path.Length; vertex++)
            {
                distance += Vector3.Distance(path[vertex - 1], path[vertex]);
                if (vertex == path.Length - 1) break;
                Vector2 incoming = Xz(path[vertex] - path[vertex - 1]).normalized;
                Vector2 outgoing = Xz(path[vertex + 1] - path[vertex]).normalized;
                int count = 0;
                foreach (var point in points)
                {
                    if (point.BendIndex != vertex) continue;
                    count++;
                    Assert.AreEqual(distance, point.Distance, 0.001f);
                    Vector2 offset = Xz(point.Position - path[vertex]);
                    float turn = Cross(incoming, outgoing);
                    Assert.Less(Cross(incoming, offset) * turn, 0f);
                    Assert.Less(Cross(outgoing, offset) * turn, 0f);
                }
                Assert.AreEqual(Vector2.Angle(incoming, outgoing) > 20f ? 1 : 0, count, $"Desvío {vertex}");
            }
            Assert.LessOrEqual(distance - points[points.Count - 1].Distance, 25f);
        }

        [Test]
        public void LayoutIsDeterministicForEveryRouteAndVariesWithSeed()
        {
            var terrain = new RouteTerrain();
            for (int route = 0; route < RouteLayout.Count - 1; route++)
            {
                var path = RouteLayout.Path(route);
                var first = TrailBirdLayout.Points(path, terrain, 713 + route);
                var second = TrailBirdLayout.Points(path, terrain, 713 + route);
                CollectionAssert.AreEqual(first, second);
                Assert.AreNotEqual(first[0].Position, TrailBirdLayout.Points(path, terrain, 900 + route)[0].Position);
            }
        }

        [Test]
        public void RiverChainWorksForManySeedsWithoutPuttingARegularMarkerOnTheCrossing()
        {
            var terrain = new RouteTerrain();
            for (int seed = 0; seed < 100; seed++)
            {
                var points = TrailBirdLayout.Points(RouteLayout.Path(5), terrain, seed);
                for (int i = 0; i < points.Count; i++)
                {
                    Assert.IsFalse(terrain.IsWater(points[i].Position.x, points[i].Position.z));
                    Assert.IsFalse(points[i].Position.z >= 116f && points[i].Position.z <= 130f);
                    if (i > 0) Assert.LessOrEqual(Vector3.Distance(points[i].Position, points[i - 1].Position), 25f);
                }
            }
        }

        [Test]
        public void RegularMarkersKeepAlternatingAcrossBendsForManySeeds()
        {
            var terrain = new RouteTerrain();
            for (int seed = 0; seed < 100; seed++)
            for (int route = 0; route < RouteLayout.Count - 1; route++)
            {
                var path = RouteLayout.Path(route);
                var points = TrailBirdLayout.Points(path, terrain, seed);
                int previousSide = 0;
                foreach (var point in points)
                {
                    if (point.BendIndex >= 0) continue;
                    float distance = 0f;
                    int segment = 0;
                    while (segment < path.Length - 2 && distance + Vector3.Distance(path[segment], path[segment + 1]) < point.Distance)
                    {
                        distance += Vector3.Distance(path[segment], path[segment + 1]);
                        segment++;
                    }
                    float side = Cross(Xz(path[segment + 1] - path[segment]), Xz(point.Position - path[segment]));
                    int actualSide = side > 0f ? 1 : -1;
                    if (previousSide != 0) Assert.AreEqual(-previousSide, actualSide, $"Sendero {route}, semilla {seed}");
                    previousSide = actualSide;
                }
            }
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(23)]
        public void FillingAStraightRiverGapPreservesDryPositionsAndAlternatingSides(int seed)
        {
            var terrain = new RouteTerrain();
            var path = new[] { new Vector3(44f, 0f, 92f), new Vector3(44f, 0f, 148f) };
            var points = TrailBirdLayout.Points(path, terrain, seed);
            Assert.IsNotEmpty(points);
            for (int i = 0; i < points.Count; i++)
            {
                Assert.IsFalse(terrain.IsWater(points[i].Position.x, points[i].Position.z));
                Assert.IsFalse(points[i].Position.z >= 116f && points[i].Position.z <= 130f);
                if (i == 0) continue;
                Assert.LessOrEqual(Vector3.Distance(points[i - 1].Position, points[i].Position), 25f);
                Assert.Less((points[i - 1].Position.x - 44f) * (points[i].Position.x - 44f), 0f);
            }
        }

        static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
