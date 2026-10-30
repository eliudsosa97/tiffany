using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class RouteTerrainTests
    {
        readonly RouteTerrain terrain = new RouteTerrain();

        [Test]
        public void RampCandleStandsBesideTheHutToLakeRampOnDryGround()
        {
            var candle = terrain.RampCandle();
            var points = RouteLayout.Path(4);
            float nearest = float.PositiveInfinity;
            for (int i = 1; i < points.Length; i++)
                nearest = Mathf.Min(nearest, RouteTerrain.SegmentDistance(new Vector2(candle.x, candle.z),
                    new Vector2(points[i - 1].x, points[i - 1].z), new Vector2(points[i].x, points[i].z)));
            Assert.AreEqual(RouteTerrain.RampCandleOffset, nearest, 0.05f, "Junto al sendero, fuera de su franja.");
            Assert.Greater(nearest - CandleSite.DefaultRadius, RouteLayout.PathWidth * 0.5f,
                "La zona segura no toca el sendero.");
            Assert.IsFalse(terrain.IsWater(candle.x, candle.z));
            Assert.AreEqual(terrain.Height(candle.x, candle.z), candle.y, 0.001f);
            Vector3 middle = (points[1] + points[2]) * 0.5f;
            Assert.Less(Mathf.Abs(candle.y - middle.y), 1.5f, "Alcanzable a pie desde la rampa.");
            Assert.IsFalse(terrain.InClearingSquare(candle.x, candle.z), "Fuera de los claros.");
            float hut = Vector2.Distance(new Vector2(candle.x, candle.z), new Vector2(RouteLayout.Clearing(4).x, RouteLayout.Clearing(4).z));
            float lake = Vector2.Distance(new Vector2(candle.x, candle.z), new Vector2(RouteLayout.Clearing(5).x, RouteLayout.Clearing(5).z));
            Assert.Less(Mathf.Abs(hut - lake), 20f, "A media rampa entre la choza y el lago.");
        }

        [Test]
        public void ClearingCoresAreFlatIrregularAndKeepTheirLocalLandmarks()
        {
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var center = RouteLayout.Clearing(i);
                float scale = RouteLayout.ClearingSizeOf(i) / RouteLayout.ClearingSize;
                float smallest = RouteLayout.ClearingSizeOf(i) * 0.5f, largest = 0f;
                for (int degrees = 0; degrees < 360; degrees++)
                {
                    float angle = degrees * Mathf.Deg2Rad;
                    float radius = terrain.ClearingRadius(i, angle);
                    smallest = Mathf.Min(smallest, radius);
                    largest = Mathf.Max(largest, radius);
                    Assert.That(radius, Is.InRange(13f * scale, 17.5f * scale));
                    for (float r = 0f; r < radius; r += 0.5f)
                    {
                        float x = center.x + Mathf.Cos(angle) * r, z = center.z + Mathf.Sin(angle) * r;
                        Assert.IsTrue(terrain.IsWalkable(x, z), $"Claro {i}, {x}, {z}");
                        Assert.AreEqual(center.y, terrain.Height(x, z), 0.0001f);
                    }
                }
                Assert.Greater(largest - smallest, 0.2f * scale);
                foreach (var local in new[] { new Vector2(0f, -13f * scale), new Vector2(-6f, -8f), new Vector2(0f, 10f * scale) })
                    Assert.IsTrue(terrain.IsWalkable(center.x + local.x, center.z + local.y));
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        public void HutPathsMeetTheNewEdgeAcrossTheirWidthWithoutSteps(int from)
        {
            var path = RouteLayout.Path(from);
            Vector3 edge = from == 3 ? path[path.Length - 1] : path[0];
            Vector3 outward = from == 3 ? Vector3.back : Vector3.left;
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            Vector3 local = edge - RouteLayout.Clearing(4);
            Assert.AreEqual(12f, Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)));
            for (int lane = -1; lane <= 1; lane++)
            for (float offset = -1f; offset <= 1f; offset += 0.125f)
            {
                Vector3 point = edge + side * (lane * 2f) + outward * offset;
                Vector3 next = point + outward * 0.125f;
                Assert.IsTrue(terrain.IsWalkable(point.x, point.z));
                Assert.LessOrEqual(Mathf.Abs(terrain.Height(point.x, point.z) - terrain.Height(next.x, next.z)),
                    Mathf.Tan(RouteLayout.CliffPathMaxSlopeDegrees * Mathf.Deg2Rad) * 0.125f + 0.0001f);
                if (offset == 0f) Assert.AreEqual(6f, terrain.Height(point.x, point.z), 0.0001f);
            }
            Assert.IsFalse(terrain.InClearingSquare(117f, 60f));
            Assert.IsTrue(terrain.IsHauntedForest(117f, 60f));
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
            Assert.IsTrue(reached.Contains(new Vector2Int(Mathf.RoundToInt((terrain.Road.xMax - 1f) * 2f),
                Mathf.RoundToInt(terrain.RoadCenterZ(terrain.Road.xMax - 1f) * 2f))));
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
            Assert.AreEqual(terrain.Bounds.xMax, terrain.Road.xMax);
            Assert.AreEqual(8f, terrain.Road.height);
            Assert.AreEqual(RouteLayout.Clearing(6).z, terrain.Road.center.y);
            Assert.AreEqual(terrain.Road.yMax, terrain.Town.yMin);
            for (float x = terrain.Road.xMin; x < terrain.Road.xMax - 0.5f; x += 0.5f)
            {
                float z = terrain.RoadCenterZ(x);
                Assert.IsTrue(terrain.IsWalkable(x, z));
                Assert.AreEqual(0f, terrain.Height(x, z), 0.001f);
            }
            Assert.AreEqual(terrain.FenceStart.x, terrain.FenceEnd.x);
            Assert.AreEqual(terrain.Road.yMax, terrain.FenceEnd.y);
            Assert.AreEqual(terrain.RiverPoint(0), terrain.FenceStart);
            RouteLayout.Gate(out var gate, out float yaw, out float width);
            Assert.AreEqual(terrain.FenceStart.x, gate.x);
            Assert.AreEqual(terrain.Road.center.y, gate.z);
            Assert.AreEqual(90f, yaw);
            Assert.AreEqual(RouteTerrain.RoadWidth, width);
            Assert.Less(terrain.LakeDistance(terrain.Bounds.xMin, RouteTerrain.LakeCenter.y), -6f);
            Assert.GreaterOrEqual(terrain.Bounds.yMax - terrain.Town.yMax, RouteTerrain.Margin);
        }

        [Test]
        public void SmallTownContainsTiffanyNorthOfTheRoadAndKeepsTheMapBounds()
        {
            var house = RouteLayout.Clearing(7);
            Assert.That(terrain.Town.width, Is.InRange(1f, 60f));
            Assert.That(terrain.Town.height, Is.InRange(1f, 60f));
            Assert.IsTrue(terrain.Town.Contains(new Vector2(house.x, house.z)));
            Assert.AreEqual(Rect.MinMaxRect(-118f, -98f, 218f, 262f), terrain.Bounds);
            for (float x = terrain.Town.xMin; x <= terrain.Town.xMax; x++)
                Assert.GreaterOrEqual(terrain.Town.yMin, terrain.RoadCenterZ(x) + RouteTerrain.RoadWidth * 0.5f);
        }

        [Test]
        public void CemeteryNorthEdgeHasTenMetersOfForestBeforeTheRoadAcrossItsFullWidth()
        {
            var cemetery = RouteLayout.Clearing(0);
            float half = RouteLayout.ClearingSizeOf(0) * 0.5f;
            float north = cemetery.z + half;
            Assert.AreEqual(new Vector3(180f, 0f, 140f), cemetery);
            for (float x = cemetery.x - half; x <= cemetery.x + half; x += 0.5f)
            {
                float roadSouth = terrain.RoadCenterZ(x) - RouteTerrain.RoadWidth * 0.5f;
                Assert.GreaterOrEqual(roadSouth - north, 10f, $"Separación al norte del cementerio en x={x}");
                float density = 0f;
                for (float z = north + 0.5f; z < roadSouth; z += 0.5f)
                {
                    Assert.IsFalse(terrain.IsWalkable(x, z), $"Atajo en {x}, {z}");
                    Assert.IsFalse(terrain.InClearingSquare(x, z));
                    Assert.IsFalse(terrain.Town.Contains(new Vector2(x, z)));
                    Assert.IsFalse(terrain.IsWater(x, z));
                    if (!terrain.InClearingSquare(x, z, 3f) && terrain.DistanceToRoute(x, z) > 3.5f)
                        density += terrain.ForestDensity(x, z);
                }
                Assert.Greater(density, 0f, $"Bosque de relleno al norte del cementerio en x={x}");
            }
        }

        [Test]
        public void FormerTownStripHasForestGroundAndPositiveDensityAcrossItsWidth()
        {
            for (float x = -64f; x <= 64f; x += 16f)
            {
                float density = 0f;
                for (float z = 184f; z < 242f; z += 2f)
                {
                    Assert.IsFalse(terrain.Town.Contains(new Vector2(x, z)));
                    Assert.IsFalse(terrain.IsWalkable(x, z));
                    Assert.That(terrain.ForestGround(x, z), Is.EqualTo(GroundType.Leaves).Or.EqualTo(GroundType.Branches));
                    density += terrain.ForestDensity(x, z);
                }
                Assert.Greater(density, 0f, $"Franja norte en x={x}");
            }
        }

        [Test]
        public void IrregularLakeHasOneContinuousBasinAtTheWestEdgeAndKeepsTheClearingOnShore()
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            float smallest = float.PositiveInfinity, largest = 0f, shoreDistance = float.PositiveInfinity;
            var clearing = RouteLayout.Clearing(5);
            for (int degrees = 0; degrees < 360; degrees++)
            {
                Vector2 point = RouteTerrain.LakeShorePoint(degrees * Mathf.Deg2Rad);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                shoreDistance = Mathf.Min(shoreDistance, Vector2.Distance(point, new Vector2(clearing.x, clearing.z)));
                if (point.x > terrain.Bounds.xMin + 0.001f)
                {
                    Assert.AreEqual(0f, terrain.LakeDistance(point.x, point.y), 0.0001f);
                    Vector2 delta = point - RouteTerrain.LakeCenter;
                    float radius = new Vector2(delta.x / RouteTerrain.LakeRadius.x, delta.y / RouteTerrain.LakeRadius.y).magnitude;
                    smallest = Mathf.Min(smallest, radius);
                    largest = Mathf.Max(largest, radius);
                }
                for (int sample = 0; sample < 50; sample++)
                {
                    Vector2 inside = Vector2.Lerp(RouteTerrain.LakeCenter, point, sample / 50f);
                    Assert.Less(terrain.LakeDistance(inside.x, inside.y), 0f);
                    Assert.Less(terrain.GroundHeight(inside.x, inside.y), 0f);
                    Assert.IsFalse(terrain.IsWalkable(inside.x, inside.y));
                }
            }
            Assert.AreEqual(terrain.Bounds.xMin, min.x, 0.001f);
            Assert.Greater(max.x - min.x, (max.y - min.y) * 1.5f);
            Assert.Greater(largest - smallest, 0.2f);
            Assert.That(shoreDistance, Is.InRange(18f, 27f));
        }

        [Test]
        public void ForestDensityBuildsAWallBesideTheRouteAndThinsTheFarForest()
        {
            float near = 0f, far = 0f;
            int nearSamples = 0, farSamples = 0;
            for (float z = terrain.Bounds.yMin + 1f; z < terrain.Bounds.yMax; z += 3f)
            for (float x = terrain.Bounds.xMin + 1f; x < terrain.Bounds.xMax; x += 3f)
            {
                if (terrain.IsHauntedForest(x, z)) continue;
                float distance = terrain.DistanceToRoute(x, z);
                if (distance > 1f && distance < 6f) { near += terrain.ForestDensity(x, z); nearSamples++; }
                else if (distance > RouteTerrain.WallDistance) { far += terrain.ForestDensity(x, z); farSamples++; }
            }
            Assert.Greater(near / nearSamples, 0.5f);
            Assert.Less(far / farSamples, 0.12f);
        }

        [Test]
        public void ForestDensityIsDeterministicDenserAroundTheHutAndHasSixMeterHolesInsidePatches()
        {
            var repeat = new RouteTerrain();
            float haunted = 0f, rest = 0f;
            int hauntedSamples = 0, restSamples = 0, holes = 0;
            for (float z = Mathf.Ceil(terrain.Bounds.yMin / 6f) * 6f; z < terrain.Bounds.yMax - 6f; z += 6f)
            for (float x = Mathf.Ceil(terrain.Bounds.xMin / 6f) * 6f; x < terrain.Bounds.xMax - 6f; x += 6f)
            {
                float density = terrain.ForestDensity(x + 3f, z + 3f);
                Assert.That(density, Is.InRange(0f, 1f));
                Assert.AreEqual(density, repeat.ForestDensity(x + 3f, z + 3f));
                if (terrain.IsHauntedForest(x + 3f, z + 3f)) { haunted += density; hauntedSamples++; }
                else { rest += density; restSamples++; }
                int neighbors = 0;
                foreach (var side in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                    if (terrain.ForestDensity(x + 3f + side.x * 6f, z + 3f + side.y * 6f) > 0f) neighbors++;
                if (neighbors < 2) continue;
                bool empty = true;
                for (int dz = 0; dz < 6; dz++)
                for (int dx = 0; dx < 6; dx++) empty &= terrain.ForestDensity(x + dx + 0.5f, z + dz + 0.5f) == 0f;
                if (empty) holes++;
            }
            Assert.Greater(haunted / hauntedSamples, rest / restSamples * 1.5f);
            Assert.Greater(holes, 10);
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
            Assert.That(tracks.x - RouteTerrain.LakeShorePoint(0f).x, Is.InRange(0f, 20f));
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
