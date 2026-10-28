using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class RouteTerrainTests
    {
        readonly RouteTerrain terrain = new RouteTerrain();

        [Test]
        public void ClearingCoresAreFlatIrregularAndKeepTheirLocalLandmarks()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var center = RouteLayout.Clearing(i);
                float smallest = 18f, largest = 0f;
                for (int degrees = 0; degrees < 360; degrees++)
                {
                    float angle = degrees * Mathf.Deg2Rad;
                    float radius = terrain.ClearingRadius(i, angle);
                    smallest = Mathf.Min(smallest, radius);
                    largest = Mathf.Max(largest, radius);
                    Assert.That(radius, Is.InRange(13f, 17.5f));
                    for (float r = 0f; r < radius; r += 0.5f)
                    {
                        float x = center.x + Mathf.Cos(angle) * r, z = center.z + Mathf.Sin(angle) * r;
                        Assert.IsTrue(terrain.IsWalkable(x, z), $"Claro {i}, {x}, {z}");
                        Assert.AreEqual(center.y, terrain.Height(x, z), 0.0001f);
                    }
                }
                Assert.Greater(largest - smallest, 0.2f);
                foreach (var local in new[] { new Vector2(0f, -13f), new Vector2(-6f, -8f), new Vector2(0f, 10f) })
                    Assert.IsTrue(terrain.IsWalkable(center.x + local.x, center.z + local.y));
            }
        }

        [Test]
        public void EveryHalfMeterPathSampleKeepsItsSlopeLimitAndNoFifteenCentimeterSteps()
        {
            var descent = RouteLayout.Path(4);
            for (float z = terrain.Bounds.yMin; z <= terrain.Bounds.yMax; z += 0.5f)
            for (float x = terrain.Bounds.xMin; x <= terrain.Bounds.xMax; x += 0.5f)
            {
                if (!terrain.IsWalkable(x, z)) continue;
                float height = terrain.Height(x, z);
                bool east = terrain.IsWalkable(x + 0.5f, z), north = terrain.IsWalkable(x, z + 0.5f);
                float dx = east ? terrain.Height(x + 0.5f, z) - height : 0f;
                float dz = north ? terrain.Height(x, z + 0.5f) - height : 0f;
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)), 0.15f, $"Salto en {x}, {z}");
                float limit = RouteLayout.MaxSlopeDegrees;
                for (int i = 1; i < descent.Length; i++)
                    if (RouteTerrain.SegmentDistance(new Vector2(x, z), new Vector2(descent[i - 1].x, descent[i - 1].z),
                        new Vector2(descent[i].x, descent[i].z)) < RouteLayout.PathWidth) limit = RouteLayout.CliffPathMaxSlopeDegrees;
                float maximum = Mathf.Tan(limit * Mathf.Deg2Rad);
                Assert.LessOrEqual(new Vector2(dx, dz).magnitude / 0.5f, maximum + 0.0001f, $"Pendiente en {x}, {z}");
            }
        }

        [Test]
        public void AllClearingsAndTheRoadShareOneWalkableComponentThroughTheCrossing()
        {
            var reached = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            var first = RouteLayout.Clearing(0);
            var start = new Vector2Int(Mathf.RoundToInt(first.x * 2f), Mathf.RoundToInt(first.z * 2f));
            reached.Add(start);
            pending.Enqueue(start);
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (pending.Count > 0)
            {
                var point = pending.Dequeue();
                foreach (var direction in directions)
                {
                    var next = point + direction;
                    if (reached.Contains(next) || !terrain.IsWalkable(next.x * 0.5f, next.y * 0.5f)) continue;
                    reached.Add(next);
                    pending.Enqueue(next);
                }
            }
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var center = RouteLayout.Clearing(i);
                Assert.IsTrue(reached.Contains(new Vector2Int(Mathf.RoundToInt(center.x * 2f), Mathf.RoundToInt(center.z * 2f))), $"Claro {i}");
                foreach (var opening in RouteLayout.Openings(i))
                    Assert.IsTrue(reached.Contains(new Vector2Int(Mathf.RoundToInt(opening.x * 2f), Mathf.RoundToInt(opening.z * 2f))));
            }
            Assert.IsTrue(reached.Contains(new Vector2Int(Mathf.RoundToInt((terrain.Road.xMin + 1f) * 2f), 312)));
        }

        [Test]
        public void RiverRunsWestFromCemeteryAndOnlyTheStoneCrossingIsAtGroundLevel()
        {
            Assert.Less(terrain.RiverPoint(0).x, RouteLayout.Clearing(0).x - 18f);
            Assert.AreEqual(terrain.Bounds.xMin, terrain.RiverPoint(terrain.RiverPointCount - 1).x);
            for (int i = 1; i < terrain.RiverPointCount; i++)
            {
                Vector2 a = terrain.RiverPoint(i - 1), b = terrain.RiverPoint(i);
                Assert.Less(b.x, a.x);
                var side = new Vector2(b.y - a.y, a.x - b.x).normalized;
                int samples = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
                for (int j = 0; j <= samples; j++)
                for (float lane = -1.5f; lane <= 1.5f; lane += 0.5f)
                {
                    var point = Vector2.Lerp(a, b, (float)j / samples) + side * lane;
                    bool crossing = Mathf.Abs(point.x - terrain.Crossing.x) <= RouteTerrain.CrossingWidth * 0.5f;
                    Assert.AreEqual(crossing, terrain.IsWalkable(point.x, point.y), $"Río {point}");
                    Assert.Less(point.y, terrain.Road.yMin);
                }
            }
            Assert.AreEqual(terrain.Crossing.y - 1.5f, terrain.GroundHeight(terrain.Crossing.x, terrain.Crossing.z), 0.001f);
        }

        [Test]
        public void LakeCliffRoadTownAndClosedFenceMatchTheMap()
        {
            Assert.Less(RouteTerrain.LakeCenter.x, RouteLayout.Clearing(5).x);
            Assert.Less(RouteTerrain.LakeCenter.y, RouteLayout.Clearing(5).z);
            Assert.IsFalse(terrain.IsWalkable(RouteTerrain.LakeCenter.x, RouteTerrain.LakeCenter.y));
            Assert.Less(terrain.Height(RouteTerrain.LakeCenter.x, RouteTerrain.LakeCenter.y), -1.5f);
            Assert.GreaterOrEqual(terrain.Height(RouteTerrain.Plateau.x, RouteTerrain.Plateau.y) - RouteLayout.Clearing(5).y, 6f);
            Assert.Greater(terrain.Height(RouteTerrain.Plateau.x, RouteTerrain.Plateau.y)
                - terrain.Height(RouteTerrain.CliffFoot.x, RouteTerrain.CliffFoot.y), 5f);
            Assert.AreEqual(terrain.Bounds.xMin, terrain.Road.xMin);
            Assert.AreEqual(terrain.FenceStart.x, terrain.Road.xMax);
            Assert.AreEqual(8f, terrain.Road.height);
            Assert.AreEqual(terrain.Road.yMax, terrain.Town.yMin);
            for (float x = terrain.Road.xMin; x < terrain.Road.xMax - 0.5f; x += 0.5f)
            {
                Assert.IsTrue(terrain.IsWalkable(x, terrain.Road.center.y));
                Assert.AreEqual(0f, terrain.Height(x, terrain.Road.center.y), 0.001f);
            }
            Assert.AreEqual(terrain.FenceStart.x, terrain.FenceEnd.x);
            Assert.AreEqual(terrain.Bounds.yMax, terrain.FenceEnd.y);
            Assert.AreEqual(terrain.RiverPoint(0), terrain.FenceStart);
            RouteLayout.Gate(out var gate, out float yaw, out float width);
            Assert.AreEqual(terrain.FenceStart.x, gate.x);
            Assert.AreEqual(terrain.Road.center.y, gate.z);
            Assert.AreEqual(90f, yaw);
            Assert.AreEqual(RouteTerrain.RoadWidth, width);
            Assert.GreaterOrEqual(RouteTerrain.LakeCenter.x - RouteTerrain.LakeRadius.x - terrain.Bounds.xMin, RouteTerrain.Margin);
            Assert.GreaterOrEqual(terrain.Bounds.yMax - terrain.Town.yMax, RouteTerrain.Margin);
        }

        [Test]
        public void ForestGroundIsDeterministicWithinTilesAndNoisierThanDirt()
        {
            var types = new HashSet<GroundType>();
            for (float z = terrain.Bounds.yMin; z < terrain.Bounds.yMax; z += RouteTerrain.ForestTileSize)
            for (float x = terrain.Bounds.xMin; x < terrain.Bounds.xMax; x += RouteTerrain.ForestTileSize)
            {
                GroundType type = terrain.ForestGround(x + 1f, z + 1f);
                Assert.That(type, Is.EqualTo(GroundType.Leaves).Or.EqualTo(GroundType.Branches));
                Assert.AreEqual(type, terrain.ForestGround(x + 31f, z + 31f));
                Assert.Greater(NoiseModel.StepRadius(type, Gait.Walk, false), NoiseModel.StepRadius(GroundType.Dirt, Gait.Walk, false));
                types.Add(type);
            }
            Assert.AreEqual(2, types.Count);
        }

        [Test]
        public void TrainLineConnectsTheTracksToTheForestSouthOfTheRoadWithGentleRamps()
        {
            var tracks = RouteLayout.Clearing(2);
            Assert.AreEqual(terrain.Bounds.center.x, tracks.x, 0.001f);
            Assert.That(tracks.x - RouteTerrain.LakeCenter.x - RouteTerrain.LakeRadius.x, Is.InRange(0f, 20f));
            Assert.Less(RouteLayout.TrainNorthEnd, terrain.Road.yMin);
            Assert.Less(RouteLayout.TrainNorthEnd, terrain.Town.yMin);
            Assert.That(RouteLayout.TrainWidth, Is.InRange(2.5f, 3f));
            Assert.AreEqual(tracks.y, terrain.TrainHeight(tracks.z), 0.001f);
            Assert.AreEqual(terrain.GroundHeight(tracks.x, RouteLayout.TrainNorthEnd), terrain.TrainHeight(RouteLayout.TrainNorthEnd), 0.001f);
            for (float z = tracks.z; z < RouteLayout.TrainNorthEnd; z += 0.5f)
            {
                float height = terrain.TrainHeight(z);
                Assert.GreaterOrEqual(height, terrain.GroundHeight(tracks.x, z) - 0.05f, $"Puente enterrado en {z}");
                if (z < tracks.z + RouteLayout.TrainSouthRampLength || z >= RouteLayout.TrainNorthEnd - RouteLayout.TrainRampLength)
                    Assert.LessOrEqual(RouteLayout.Slope(new Vector3(tracks.x, height, z),
                        new Vector3(tracks.x, terrain.TrainHeight(z + 0.5f), z + 0.5f)), 8.001f, $"Rampa en {z}");
                else Assert.That(height - Mathf.Max(0f, terrain.GroundHeight(tracks.x, z)), Is.InRange(3f, 4f));
            }
        }

        [Test]
        public void RampFollowsTheCliffWithoutCuttingAnotherClearing()
        {
            var path = RouteLayout.Path(4);
            float length = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                length += Vector2.Distance(new Vector2(path[i].x, path[i].z), new Vector2(path[i - 1].x, path[i - 1].z));
                for (int sample = 0; sample <= 100; sample++)
                {
                    Vector3 point = Vector3.Lerp(path[i - 1], path[i], sample / 100f);
                    Assert.IsTrue(terrain.IsWalkable(point.x, point.z));
                }
            }
            Assert.Greater(length, 6f / Mathf.Tan(8f * Mathf.Deg2Rad));
            Assert.Less(length, 70f);
            Assert.AreEqual(6f, path[0].y - path[path.Length - 1].y, 0.001f);
        }
    }
}
