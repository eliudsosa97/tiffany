using System.Reflection;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
    {
        [Test]
        public void LandscapeOnlyBlocksMapEdgesWaterCliffTownTreesBridgeRailsAndCandles()
        {
            var terrain = new RouteTerrain();
            var landscape = route.Find("Landscape");
            Assert.IsNull(landscape.Find("Walkable Boundary"));
            string[] allowed = { "Map Boundary", "River", "Lake Banks", "Town", "Fence", "Cliff Rocks", "Forest Fill", "Train Bridge" };
            foreach (var collider in landscape.GetComponentsInChildren<Collider>())
            {
                if (collider.TryGetComponent<GroundSurface>(out _) || collider.GetComponentInParent<CandleSpot>() != null) continue;
                Assert.IsTrue(System.Array.Exists(allowed, name => collider.transform.IsChildOf(landscape.Find(name))), collider.name);
            }
            var border = landscape.Find("Map Boundary").GetComponentsInChildren<Collider>();
            foreach (var wall in border)
            {
                Vector3 point = route.InverseTransformPoint(wall.bounds.center);
                float edge = Mathf.Min(Mathf.Abs(point.x - terrain.Bounds.xMin), Mathf.Abs(point.x - terrain.Bounds.xMax),
                    Mathf.Abs(point.z - terrain.Bounds.yMin), Mathf.Abs(point.z - terrain.Bounds.yMax));
                Assert.Less(edge, 0.01f, wall.name);
            }
            for (int side = 0; side < 4; side++)
            for (float t = 0.01f; t < 1f; t += 0.02f)
            {
                var b = terrain.Bounds;
                Vector2 point = side < 2 ? new Vector2(side == 0 ? b.xMin : b.xMax, Mathf.Lerp(b.yMin, b.yMax, t))
                    : new Vector2(Mathf.Lerp(b.xMin, b.xMax, t), side == 2 ? b.yMin : b.yMax);
                Vector3 normal = side == 0 ? Vector3.right : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.back;
                var ray = new Ray(route.TransformPoint(new Vector3(point.x, terrain.GroundHeight(point.x, point.y) + 1f, point.y) + normal), -normal);
                Assert.IsTrue(System.Array.Exists(border, wall => wall.Raycast(ray, out _, 2f)), $"Borde exterior {side}/{t}");
            }
        }

        [Test]
        public void TownFenceClosesBackAndSidesWhileHouseAndRoadRemainAccessible()
        {
            var terrain = new RouteTerrain();
            var town = terrain.Town;
            var fence = route.Find("Landscape/Fence/Town Boundary").GetComponentsInChildren<Collider>();
            Assert.IsNotEmpty(fence);
            foreach (float x in new[] { town.xMin, town.xMax })
            for (float z = 170f; z < town.yMax; z += 2f)
                Check(new Vector3(x - 1f, 1f, z), Vector3.right, true);
            for (float x = town.xMin + 2f; x < town.xMax; x += 2f)
                Check(new Vector3(x, 1f, town.yMax - 1f), Vector3.forward, true);
            Check(new Vector3(0f, 1f, town.yMin - 1f), Vector3.forward, true);
            Check(new Vector3(120f, 1f, town.yMin - 1f), Vector3.forward, false);
            Check(new Vector3(100f, 1f, 160f), Vector3.right, false);
            void Check(Vector3 point, Vector3 direction, bool blocked)
            {
                point.y = terrain.GroundHeight(point.x + direction.x, point.z + direction.z) + 1f;
                var ray = new Ray(route.TransformPoint(point), direction);
                Assert.AreEqual(blocked, System.Array.Exists(fence, wall => wall.Raycast(ray, out _, 3f)), point.ToString());
            }
        }

        [Test]
        public void FootstepsReadTheThinPathAndClearingFloorsBeforeForestTerrain()
        {
            var terrain = new RouteTerrain();
            var find = typeof(PlayerController).GetMethod("FindGround", BindingFlags.Instance | BindingFlags.NonPublic);
            GroundType Read(Vector3 point)
            {
                player.transform.position = route.TransformPoint(point);
                Physics.SyncTransforms();
                return (GroundType)find.Invoke(player, null);
            }
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var clearing = route.GetChild(i);
                var ground = clearing.Find("Ground").GetComponent<GroundSurface>();
                Assert.AreEqual(ground.type, Read(RouteLayout.Clearing(i) + Vector3.back * 13f), $"Claro {i}");
                if (i == RouteLayout.Count - 1) continue;
                var points = RouteLayout.Path(i);
                Vector3 point = (points[0] + points[1]) * 0.5f;
                point.y = terrain.Height(point.x, point.z);
                Assert.AreEqual(GroundType.Dirt, Read(point), $"Sendero {i}");
            }
            var river = route.GetChild(5).Find("Path to Next Clearing/River Crossing/River");
            foreach (string name in new[] { "Stone 1", "Stone 2", "Stone 4", "Stone 5", "Log" })
            {
                var surface = river.Find(name).GetComponent<GroundSurface>();
                Vector3 point = surface.GetComponent<Collider>().bounds.center;
                point.y = surface.GetComponent<Collider>().bounds.max.y;
                Assert.AreEqual(surface.type, Read(route.InverseTransformPoint(point)), name);
            }
            var found = new System.Collections.Generic.HashSet<GroundType>();
            foreach (var collider in route.Find("Landscape/Terrain").GetComponentsInChildren<MeshCollider>())
            {
                if (!collider.name.EndsWith(" Wild")) continue;
                var mesh = collider.sharedMesh;
                var indices = mesh.triangles;
                var vertices = mesh.vertices;
                Vector3 point = (vertices[indices[0]] + vertices[indices[1]] + vertices[indices[2]]) / 3f;
                if (terrain.DistanceToRoute(point.x, point.z) < 10f || terrain.TrainDistance(point.x, point.z) < 4f) continue;
                GroundType expected = terrain.ForestGround(point.x, point.z);
                if (Read(point) == expected) found.Add(expected);
            }
            CollectionAssert.AreEquivalent(new[] { GroundType.Leaves, GroundType.Branches }, found);
        }

        [Test]
        public void FillHasOneSolidTrunkAndOneCoverColliderPerInstancedTree()
        {
            var fill = route.Find("Landscape/Forest Fill");
            var terrain = new RouteTerrain();
            float farthest = 0f;
            int trees = 0;
            foreach (var cell in fill.GetComponentsInChildren<ForestInstances>())
            {
                int trunks = 0, crowns = 0;
                foreach (var batch in cell.data.batches)
                {
                    bool trunk = batch.mesh.name.StartsWith("BugambiliasOak") || batch.mesh.name == "Ocote0";
                    bool crown = batch.mesh.name.StartsWith("BugambiliasLeaves") || batch.mesh.name == "OcoteCanopy0";
                    if (trunk) trunks += batch.matrices.Length;
                    if (crown) crowns += batch.matrices.Length;
                    if (!trunk) continue;
                    foreach (var matrix in batch.matrices)
                    {
                        var point = cell.transform.localPosition + matrix.MultiplyPoint3x4(Vector3.zero);
                        farthest = Mathf.Max(farthest, terrain.DistanceToRoute(point.x, point.z));
                    }
                }
                var solids = cell.GetComponents<CapsuleCollider>();
                var cover = cell.GetComponentsInChildren<BoxCollider>();
                Assert.AreEqual(trunks, solids.Length);
                Assert.AreEqual(trunks, crowns);
                Assert.AreEqual(crowns, cover.Length);
                Assert.AreEqual(NavMesh.GetAreaFromName("Not Walkable"), cell.GetComponent<NavMeshModifier>().area);
                foreach (var solid in solids)
                {
                    Assert.IsFalse(solid.isTrigger);
                    Assert.Greater(solid.radius, 0.25f);
                    var foot = cell.transform.localPosition + solid.center - Vector3.up * (solid.height * 0.5f);
                    Assert.AreEqual(terrain.GroundHeight(foot.x, foot.z), foot.y, 0.001f);
                }
                foreach (var canopy in cover)
                {
                    Assert.AreEqual(LayerMask.NameToLayer("Cover"), canopy.gameObject.layer);
                    Assert.IsFalse(canopy.isTrigger);
                    Assert.IsTrue(canopy.GetComponent<NavMeshModifier>().ignoreFromBuild);
                    Vector3 point = route.InverseTransformPoint(canopy.bounds.center);
                    float ground = terrain.GroundHeight(point.x, point.z);
                    Assert.Greater(canopy.bounds.min.y - route.position.y - ground, 2.5f);
                }
                trees += trunks;
            }
            Assert.Greater(trees, 100);
            Assert.Greater(farthest, 40f);
        }

        [Test]
        public void FillTreesCrowdTheRouteMoreThanTheFarForest()
        {
            var terrain = new RouteTerrain();
            int nearTrees = 0, farTrees = 0, nearArea = 0, farArea = 0;
            foreach (var cell in route.Find("Landscape/Forest Fill").GetComponentsInChildren<ForestInstances>())
            foreach (var batch in cell.data.batches)
            {
                if (batch.mesh.name != "Ocote0" && !batch.mesh.name.StartsWith("BugambiliasOak")) continue;
                foreach (var matrix in batch.matrices)
                {
                    Vector3 point = cell.transform.localPosition + matrix.MultiplyPoint3x4(Vector3.zero);
                    if (terrain.DistanceToRoute(point.x, point.z) <= RouteTerrain.WallDistance) nearTrees++;
                    else farTrees++;
                }
            }
            for (float z = terrain.Bounds.yMin + 2f; z < terrain.Bounds.yMax; z += 4f)
            for (float x = terrain.Bounds.xMin + 2f; x < terrain.Bounds.xMax; x += 4f)
            {
                float distance = terrain.DistanceToRoute(x, z);
                if (distance <= 2f || terrain.InClearingSquare(x, z)) continue;
                if (distance <= RouteTerrain.WallDistance) nearArea++;
                else farArea++;
            }
            Assert.Greater((float)nearTrees / nearArea, (float)farTrees / farArea * 3f);
        }

        [Test]
        public void FillClustersLeaveSixMeterGapsAndTheHauntedRingHasMoreLargeTreesPerArea()
        {
            var terrain = new RouteTerrain();
            var occupied = new System.Collections.Generic.HashSet<Vector2Int>();
            int hauntedTrees = 0, otherTrees = 0, hauntedArea = 0, otherArea = 0, gaps = 0;
            foreach (var cell in route.Find("Landscape/Forest Fill").GetComponentsInChildren<ForestInstances>())
            foreach (var batch in cell.data.batches)
            {
                bool pine = batch.mesh.name == "Ocote0";
                if (!pine && !batch.mesh.name.StartsWith("BugambiliasOak")) continue;
                foreach (var matrix in batch.matrices)
                {
                    Vector3 point = cell.transform.localPosition + matrix.MultiplyPoint3x4(Vector3.zero);
                    Assert.Greater(terrain.ForestDensity(point.x, point.z), 0f);
                    occupied.Add(new Vector2Int(Mathf.FloorToInt(point.x / 6f), Mathf.FloorToInt(point.z / 6f)));
                    if (!terrain.IsHauntedForest(point.x, point.z)) { otherTrees++; continue; }
                    hauntedTrees++;
                    Assert.IsTrue(pine || batch.mesh.name == "BugambiliasOak1");
                    Assert.AreEqual(pine ? "ForestPineBark" : "ForestOakBark", batch.material.name);
                    Assert.That(matrix.MultiplyVector(Vector3.up).magnitude / (pine ? 0.44f : 1f), Is.InRange(1.25f, 1.4f));
                }
            }
            for (float z = terrain.Bounds.yMin + 3f; z < terrain.Bounds.yMax; z += 6f)
            for (float x = terrain.Bounds.xMin + 3f; x < terrain.Bounds.xMax; x += 6f)
            {
                if (terrain.DistanceToRoute(x, z) <= 3.5f || terrain.InClearingSquare(x, z, 3f)
                    || RouteTerrain.RectangleDistance(new Vector2(x, z), terrain.Town) <= 3f
                    || terrain.LakeDistance(x, z) <= 4f || terrain.RiverDistance(x, z) <= 5f
                    || terrain.TrainDistance(x, z) <= 4.9f) continue;
                if (terrain.IsHauntedForest(x, z)) hauntedArea++;
                else otherArea++;
            }
            foreach (var cell in occupied)
            {
                var gap = cell + Vector2Int.right;
                if (occupied.Contains(gap)) continue;
                int neighbors = 0;
                for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                    if (occupied.Contains(gap + new Vector2Int(x, z))) neighbors++;
                if (neighbors >= 3) gaps++;
            }
            Assert.Greater(hauntedArea, 0);
            Assert.Greater(otherArea, 0);
            Assert.Greater((float)hauntedTrees / hauntedArea, (float)otherTrees / otherArea * 1.5f);
            Assert.Greater(gaps, 5);
        }

        [Test]
        public void TrainDeckHasSolidGuardrailsAndTwoRampsOfAtMostEightDegrees()
        {
            var terrain = new RouteTerrain();
            var train = route.Find("Landscape/Train Bridge");
            Assert.IsNotNull(train);
            foreach (string name in new[] { "South Ramp", "Deck", "North Ramp" })
            {
                var section = train.Find(name);
                var floor = section.Find("Ground").GetComponent<MeshCollider>();
                Assert.AreEqual(GroundType.Gravel, GroundSurface.TypeOf(floor));
                Assert.IsNull(floor.GetComponent<NavMeshModifier>());
                var mesh = floor.sharedMesh;
                var areas = section.GetComponentsInChildren<NavMeshModifierVolume>();
                Assert.IsNotEmpty(areas);
                foreach (var area in areas) Assert.AreEqual(NavMesh.GetAreaFromName("Walkable"), area.area);
                Assert.AreEqual(RouteLayout.TrainWidth, mesh.bounds.size.x, 0.001f);
                var vertices = mesh.vertices;
                for (int i = 2; i < vertices.Length; i += 2)
                {
                    if (name != "Deck") Assert.LessOrEqual(RouteLayout.Slope(vertices[i - 2], vertices[i]), 8.001f, name);
                    Vector3 point = (vertices[i - 2] + vertices[i - 1] + vertices[i] + vertices[i + 1]) * 0.25f;
                    var ray = new Ray(route.TransformPoint(point + Vector3.up * 0.6f), Vector3.right);
                    var guards = System.Array.FindAll(section.GetComponentsInChildren<BoxCollider>(), c => c.name == "Guardrail");
                    Assert.AreEqual((vertices.Length / 2 - 1) * 2, guards.Length);
                    Assert.IsTrue(System.Array.Exists(guards, c => c.Raycast(ray, out _, 2f)), name);
                    ray.direction = Vector3.left;
                    Assert.IsTrue(System.Array.Exists(guards, c => c.Raycast(ray, out _, 2f)), name);
                }
            }
            var banks = route.Find("Landscape/River").GetComponentsInChildren<Collider>();
            float x = RouteLayout.Clearing(2).x;
            var across = new Ray(route.TransformPoint(new Vector3(x, terrain.TrainHeight(120f) + 0.6f, 116f)), Vector3.forward);
            Assert.IsFalse(System.Array.Exists(banks, c => c.Raycast(across, out _, 12f)));
            float previous = float.NaN;
            foreach (Transform child in train)
            {
                if (child.name != "Pier") continue;
                Assert.IsNotNull(child.GetComponent<Collider>());
                if (!float.IsNaN(previous)) Assert.That(child.localPosition.z - previous, Is.InRange(8f, 10f));
                previous = child.localPosition.z;
            }
            Assert.IsFalse(float.IsNaN(previous));
        }
    }
}
