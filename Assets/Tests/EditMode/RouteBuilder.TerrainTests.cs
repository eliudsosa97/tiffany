using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
    {
        [Test]
        public void TerrainTilesHavePersistentMeshesCollidersAndNoDuplicateNavigationFloors()
        {
            var terrain = new RouteTerrain();
            var descent = RouteLayout.Path(4);
            var tiles = route.Find("Landscape/Terrain").GetComponentsInChildren<MeshCollider>();
            Assert.Greater(tiles.Length, 10);
            foreach (var tile in tiles)
            {
                var mesh = tile.sharedMesh;
                Assert.IsTrue(AssetDatabase.Contains(mesh), tile.name);
                Assert.AreSame(mesh, tile.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat);
                Assert.LessOrEqual(mesh.vertexCount, 1300);
                Assert.LessOrEqual(mesh.bounds.size.x, 64.001f);
                Assert.LessOrEqual(mesh.bounds.size.z, 64.001f);
                bool forest = tile.name.EndsWith(" Wild");
                Assert.AreEqual(forest ? terrain.ForestGround(mesh.bounds.center.x, mesh.bounds.center.z) : GroundType.Dirt,
                    tile.GetComponent<GroundSurface>().type);
                Assert.AreEqual("RouteGround", tile.GetComponent<Renderer>().sharedMaterial.name);
                if (tile.name.EndsWith(" Wild")) Assert.AreEqual(NavMesh.GetAreaFromName(RouteTerrain.ForestArea), tile.GetComponent<NavMeshModifier>().area);
                else if (tile.name.EndsWith(" Bed")) Assert.IsTrue(tile.GetComponent<NavMeshModifier>().ignoreFromBuild);
                else Assert.IsNull(tile.GetComponent<NavMeshModifier>());
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    Vector3 center = (a + b + c) / 3f;
                    if (!terrain.IsWalkable(center.x, center.z) || terrain.IsWater(center.x, center.z)) continue;
                    float limit = RouteLayout.MaxSlopeDegrees;
                    for (int segment = 1; segment < descent.Length; segment++)
                        if (RouteTerrain.SegmentDistance(new Vector2(center.x, center.z),
                            new Vector2(descent[segment - 1].x, descent[segment - 1].z),
                            new Vector2(descent[segment].x, descent[segment].z)) < RouteLayout.PathWidth)
                            limit = RouteLayout.CliffPathMaxSlopeDegrees;
                    Assert.LessOrEqual(Vector3.Angle(Vector3.up, Vector3.Cross(b - a, c - a)), limit + 0.001f, $"Pendiente en {center}");
                }
            }
            for (float z = terrain.Bounds.yMin + 1f; z < terrain.Bounds.yMax; z += 7f)
            for (float x = terrain.Bounds.xMin + 1f; x < terrain.Bounds.xMax; x += 7f)
            {
                var ray = new Ray(route.TransformPoint(new Vector3(x, 20f, z)), Vector3.down);
                Assert.IsTrue(System.Array.Exists(tiles, tile => tile.Raycast(ray, out _, 40f)), $"Hueco de terreno en {x}, {z}");
            }
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                var ground = route.GetChild(i).Find("Ground");
                Assert.IsFalse(ground.GetComponent<Renderer>().enabled);
                Assert.IsTrue(ground.GetComponent<Collider>().enabled);
                Assert.IsTrue(ground.GetComponent<NavMeshModifier>().ignoreFromBuild);
                Assert.Less(ground.GetComponent<Collider>().bounds.size.y, 0.1f);
                var path = route.GetChild(i).Find("Path to Next Clearing");
                if (path == null) continue;
                foreach (var surface in path.GetComponentsInChildren<GroundSurface>())
                {
                    if (surface.type != GroundType.Dirt) continue;
                    Assert.IsTrue(surface.GetComponent<NavMeshModifier>().ignoreFromBuild);
                    Assert.IsFalse(surface.GetComponent<Renderer>().enabled);
                }
            }
        }

        [Test]
        public void RiverBanksAreSolidAlongBothSidesExceptAtTheOriginalCrossing()
        {
            var terrain = new RouteTerrain();
            var banks = route.Find("Landscape/River").GetComponentsInChildren<Collider>();
            Assert.GreaterOrEqual(banks.Length, (terrain.RiverPointCount - 1) * 2);
            for (int i = 1; i < terrain.RiverPointCount; i++)
            {
                Vector2 a = terrain.RiverPoint(i - 1), b = terrain.RiverPoint(i);
                var side = new Vector2(b.y - a.y, a.x - b.x).normalized;
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
                for (int sample = 1; sample < steps; sample++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector2 point = Vector2.Lerp(a, b, (float)sample / steps);
                    if (Mathf.Abs(point.x - terrain.Crossing.x) < 1.2f) continue;
                    var direction = new Vector3(side.x, 0f, side.y) * sign;
                    var ray = new Ray(route.TransformPoint(new Vector3(point.x, 1f, point.y) + direction * 3f), -direction);
                    Assert.IsTrue(System.Array.Exists(banks, bank => bank.Raycast(ray, out _, 2f)), $"Orilla {i}/{sample}/{sign}");
                }
            }
            var crossingRay = new Ray(route.TransformPoint(terrain.Crossing + new Vector3(0f, 1f, -4f)), Vector3.forward);
            Assert.IsFalse(System.Array.Exists(banks, bank => bank.Raycast(crossingRay, out _, 8f)));
            int riverSounds = 0;
            foreach (var sound in route.GetComponentsInChildren<AudioSource>())
                if (sound.clip != null && sound.clip.name == "river_loop") riverSounds++;
            Assert.AreEqual(1, riverSounds);
        }

        [Test]
        public void TownHasStaticSolidLotsAndTheFenceClosesTheRoadWithoutReplacingTiffanysHouse()
        {
            var terrain = new RouteTerrain();
            var houses = route.Find("Landscape/Town");
            Assert.That(houses.childCount, Is.InRange(16, 24));
            foreach (Transform house in houses)
            {
                var collider = house.GetComponent<BoxCollider>();
                Assert.IsNotNull(collider);
                Assert.IsFalse(collider.isTrigger);
                Assert.That(collider.bounds.size.x, Is.InRange(6f, 10f));
                Assert.That(collider.bounds.size.y, Is.InRange(4f, 7f));
                Assert.IsTrue(terrain.Town.Contains(new Vector2(house.localPosition.x, house.localPosition.z)));
                Assert.IsFalse(terrain.IsWalkable(house.localPosition.x, house.localPosition.z));
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(house.gameObject, StaticEditorFlags.BatchingStatic));
                Assert.AreEqual(NavMesh.GetAreaFromName("Not Walkable"), house.GetComponent<NavMeshModifier>().area);
                Assert.AreEqual("RouteStructure", house.GetComponent<Renderer>().sharedMaterial.name);
            }
            Assert.IsNotNull(route.GetChild(7).Find("House/Window"));
            Assert.IsNotNull(route.GetChild(7).Find("Window Candle"));
            Assert.IsNotNull(route.GetChild(7).Find("Tiffany Sign/Label"));
            var fence = route.Find("Landscape/Fence").GetComponentsInChildren<Collider>();
            for (float z = terrain.FenceStart.y + 1f; z < terrain.FenceEnd.y; z += 0.5f)
            {
                float height = terrain.Height(terrain.FenceStart.x, z);
                var ray = new Ray(route.TransformPoint(new Vector3(terrain.FenceStart.x - 2f, height + 0.8f, z)), Vector3.right);
                Assert.IsTrue(System.Array.Exists(fence, collider => collider.Raycast(ray, out _, 4f)), $"Cerca en z={z}");
            }
        }

        [Test]
        public void ForestFillUsesPersistentInstancingCellsWithinEveryClearingBudget()
        {
            Assert.AreEqual(250000, ForestBuilder.AreaTriangleBudget);
            var fill = route.Find("Landscape/Forest Fill");
            var cells = fill.GetComponentsInChildren<ForestInstances>();
            Assert.IsNotEmpty(cells);
            Assert.IsEmpty(fill.GetComponentsInChildren<Renderer>());
            Assert.IsNotEmpty(fill.GetComponentsInChildren<CapsuleCollider>());
            Assert.IsNotEmpty(fill.GetComponentsInChildren<BoxCollider>());
            var terrain = new RouteTerrain();
            foreach (var cell in cells)
            {
                Assert.AreEqual(0f, cell.transform.localPosition.x % ForestBuilder.CellSize, 0.001f);
                Assert.AreEqual(0f, cell.transform.localPosition.z % ForestBuilder.CellSize, 0.001f);
                Assert.IsTrue(AssetDatabase.Contains(cell.data));
                foreach (var batch in cell.data.batches)
                {
                    Assert.IsTrue(AssetDatabase.Contains(batch.mesh));
                    Assert.IsTrue(batch.material.enableInstancing);
                    StringAssert.StartsWith("Forest", batch.material.name);
                    StringAssert.StartsWith("Assets/Art/Stylized/Meshes/", AssetDatabase.GetAssetPath(batch.mesh));
                    foreach (var matrix in batch.matrices)
                    {
                        Vector3 position = cell.transform.localPosition + matrix.MultiplyPoint3x4(Vector3.zero);
                        Assert.IsFalse(terrain.IsWalkable(position.x, position.z));
                        Assert.Greater(terrain.DistanceToRoute(position.x, position.z), 0f);
                        Assert.IsFalse(terrain.Town.Contains(new Vector2(position.x, position.z)));
                        Assert.IsFalse(terrain.IsWater(position.x, position.z));
                        if (batch.mesh.name == "GoldenGrass0" || batch.mesh.name == "DryShrub0" || batch.mesh.name == "OakLitter0")
                            Assert.LessOrEqual(terrain.DistanceToRoute(position.x, position.z, false), 10f);
                        var bounds = ForestInstances.TransformBounds(batch.mesh.bounds, matrix);
                        Assert.IsTrue(batch.bounds.Contains(bounds.min));
                        Assert.IsTrue(batch.bounds.Contains(bounds.max));
                    }
                }
            }
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                long triangles = ForestBuilder.TriangleCount(route.GetChild(i).Find("Forest"));
                long extra = 0;
                Rect view = ForestBuilder.ViewSquare(i);
                foreach (var cell in cells)
                {
                    var position = cell.transform.localPosition;
                    var key = new Vector2Int(Mathf.RoundToInt(position.x / ForestBuilder.CellSize), Mathf.RoundToInt(position.z / ForestBuilder.CellSize));
                    if (view.Overlaps(ForestBuilder.CellBounds(key))) extra += ForestBuilder.TriangleCount(cell.transform);
                }
                Assert.Greater(extra, 0, $"Relleno ausente en claro {i}");
                Assert.LessOrEqual(triangles + extra, ForestBuilder.AreaTriangleBudget, $"Claro {i}: {triangles} + {extra}");
                Assert.LessOrEqual(route.GetChild(i).GetComponentsInChildren<Renderer>(true).Length, 200);
            }
        }
    }
}
