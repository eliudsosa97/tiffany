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
        [TestCase(false)]
        [TestCase(true)]
        public void ReturnFromHutCandlesLeavesTheCheckpointBehindAndUsesTheCliffTrail(bool challengeCheckpoint)
        {
            var hut = route.GetChild(4);
            var checkpoint = hut.Find("Family Candle").GetComponent<CandleSpot>();
            if (challengeCheckpoint)
                foreach (var challenge in route.GetComponentsInChildren<PowerChallenge>())
                    if (challenge.kind == ChallengeKind.Hut) checkpoint = challenge.checkpoint;
            var exclusion = checkpoint.gameObject.AddComponent<NavMeshModifierVolume>();
            exclusion.area = NavMesh.GetAreaFromName("Not Walkable");
            exclusion.center = Vector3.up;
            exclusion.size = new Vector3(checkpoint.radius * 2f, 2f, checkpoint.radius * 2f);
            var surface = route.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.layerMask = ~LayerMask.GetMask(ForestLayout.CoverLayer, "Ignore Raycast");
            surface.BuildNavMesh();
            navMeshData = surface.navMeshData;

            Vector3 position = checkpoint.transform.position + Vector3.back;
            Vector3 target = route.GetChild(5).Find("Family Candle").position + Vector3.back * 1.5f;
            int handle = SafeZones.Add(checkpoint.transform.position, checkpoint.radius);
            try
            {
                var navigation = new PlaythroughNavigation();
                Assert.IsTrue(navigation.Calculate(position, target));
                Vector2 move = navigation.Move(position, 0f, 0.02f, out _);
                Assert.Less(move.y, 0f, "Debe salir por detrás de la veladora, sin atravesar el vaso.");

                Assert.IsTrue(SafeZones.TryPushOut(position, 0.5f, out var exit));
                int walkable = 1 << NavMesh.GetAreaFromName("Walkable");
                Assert.IsTrue(NavMesh.SamplePosition(exit, out var start, 1f, walkable));
                Assert.IsTrue(NavMesh.SamplePosition(target, out var end, 0.5f, walkable));
                var path = new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, walkable, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
                var corners = path.corners;
                float length = 0f;
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = route.InverseTransformPoint(corners[i]);
                    Assert.Greater(point.z, 25f, "El regreso no debe rodear por el establo ni las vías.");
                    Assert.Less(point.z, 110f, "El regreso no debe rodear por el río ni la carretera.");
                    if (i > 0) length += Vector3.Distance(corners[i - 1], corners[i]);
                }
                Assert.Less(length, 140f, "La veladora del lago debe alcanzarse por la rampa de la choza.");
            }
            finally { SafeZones.Remove(handle); }
        }

        [Test]
        public void HutLandmarksKeepTheirPositionsAndFitInsideTwentyFourMeters()
        {
            var hut = route.GetChild(4);
            foreach (var (name, position) in new[] { ("Hut", new Vector3(0f, 0f, 5f)),
                ("Hide Hut Bush", new Vector3(-6f, 0f, 3f)), ("Memory Point", new Vector3(0f, 0f, 3f)),
                ("Family Candle", new Vector3(-6f, 0f, -8f)),
                ("Route 5 Witch Hut", new Vector3(0f, 0f, -8f)) })
            {
                var item = hut.Find(name);
                Assert.IsNotNull(item, name);
                Assert.AreEqual(position, item.localPosition, name);
                Check(item.position, name);
                foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                { Check(renderer.bounds.min, name); Check(renderer.bounds.max, name); }
                foreach (var collider in item.GetComponentsInChildren<Collider>(true))
                { Check(collider.bounds.min, name); Check(collider.bounds.max, name); }
            }
            void Check(Vector3 world, string name)
            {
                Vector3 local = hut.InverseTransformPoint(world);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)), 12f, name);
            }
        }

        [Test]
        public void FillTreesReachTheSixMeterBandExcludedByTheOldHutSquare()
        {
            var hut = route.GetChild(4);
            int trees = 0;
            foreach (var trunk in route.Find("Landscape/Forest Fill").GetComponentsInChildren<CapsuleCollider>())
            {
                Vector3 point = hut.InverseTransformPoint(trunk.transform.TransformPoint(trunk.center));
                float distance = RouteTerrain.RectangleDistance(new Vector2(point.x, point.z), new Rect(-12f, -12f, 24f, 24f));
                if (distance > 0f && distance < 6f && Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z)) < 18f) trees++;
            }
            Assert.Greater(trees, 0, "El cuadrado anterior de 36 m excluía todo el relleno de esta franja.");
        }

        [Test]
        public void HutForestFramesTheCaptureAndKeepsItsViewPointsVisible()
        {
            var hut = route.GetChild(4);
            var camera = new GameObject("Hut Camera").AddComponent<FollowCamera>();
            camera.transform.SetParent(root.transform, false);
            camera.target = player.transform;
            camera.GetComponent<Camera>().aspect = 16f / 9f;
            var canopies = route.GetComponentsInChildren<BoxCollider>();
            foreach (var point in ForestBuilder.RouteViewPoints(4))
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                player.transform.SetPositionAndRotation(hut.TransformPoint(new Vector3(point.x, 0f, point.y)), Quaternion.Euler(0f, yaw, 0f));
                camera.Snap();
                Vector3 direction = player.transform.position + Vector3.up * 0.6f - camera.transform.position;
                var ray = new Ray(camera.transform.position, direction);
                foreach (var canopy in canopies)
                    if (canopy.gameObject.layer == LayerMask.NameToLayer(ForestLayout.CoverLayer))
                        Assert.IsFalse(canopy.Raycast(ray, out _, direction.magnitude), $"Copa ante {point}, yaw {yaw}.");
            }
            player.transform.SetPositionAndRotation(hut.Find("Route 5 Witch Hut").position, Quaternion.identity);
            camera.Snap();
            var planes = GeometryUtility.CalculateFrustumPlanes(camera.GetComponent<Camera>());
            int visible = 0;
            foreach (var trunk in hut.Find("Forest").GetComponentsInChildren<CapsuleCollider>())
                if (GeometryUtility.TestPlanesAABB(planes, trunk.GetComponent<Renderer>().bounds)) visible++;
            Assert.GreaterOrEqual(visible, 3, "Árboles en el encuadre de la parada de la choza.");
        }

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
                string suffix = tile.name.Substring(tile.name.LastIndexOf(' ') + 1);
                var type = forest ? terrain.ForestGround(mesh.bounds.center.x, mesh.bounds.center.z)
                    : System.Enum.TryParse(suffix, out GroundType clearingType) ? clearingType : GroundType.Dirt;
                Assert.AreEqual(type, tile.GetComponent<GroundSurface>().type);
                Assert.AreSame(GroundMaterials.For(type), tile.GetComponent<Renderer>().sharedMaterial);
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
                    Assert.IsTrue(surface.GetComponent<Collider>().enabled);
                    var renderer = surface.GetComponent<Renderer>();
                    Assert.IsTrue(renderer.enabled);
                    Assert.AreSame(GroundMaterials.For(GroundType.Dirt), renderer.sharedMaterial);
                    Assert.IsTrue(AssetDatabase.Contains(renderer.sharedMaterial));
                    if (!surface.TryGetComponent<MeshCollider>(out var floor)) continue;
                    foreach (var vertex in floor.sharedMesh.vertices)
                    {
                        Vector3 point = route.InverseTransformPoint(surface.transform.TransformPoint(vertex));
                        Assert.AreEqual(terrain.Height(point.x, point.z) + TerrainBuilder.FloorOffset, point.y, 0.001f);
                    }
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
                Assert.That(collider.bounds.size.x, Is.InRange(6f - 0.001f, 10f + 0.001f));
                Assert.That(collider.bounds.size.y, Is.InRange(4f - 0.001f, 7f + 0.001f));
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
