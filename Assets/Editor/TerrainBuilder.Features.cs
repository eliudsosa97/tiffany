using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class TerrainBuilder
    {
        static void BuildRiver(Transform parent, RouteTerrain terrain)
        {
            var material = GeneratedMaterials.GetWater(true);
            for (int i = 1; i < terrain.RiverPointCount; i++)
            {
                Vector2 a = terrain.RiverPoint(i - 1), b = terrain.RiverPoint(i);
                Vector2 middle = (a + b) * 0.5f;
                var water = SceneBuilder.CreatePrimitive($"Water {i}", PrimitiveType.Plane, parent,
                    new Vector3(middle.x, terrain.Crossing.y - 0.15f, middle.y),
                    new Vector3(RouteTerrain.RiverWidth * 0.1f, 1f, (Vector2.Distance(a, b) + 0.3f) * 0.1f), material);
                water.transform.localRotation = Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.y - a.y));
                Object.DestroyImmediate(water.GetComponent<Collider>());
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector2 first = terrain.RiverBankPoint(i - 1, sign), last = terrain.RiverBankPoint(i, sign);
                    if (Mathf.Abs(a.y - terrain.Crossing.z) < 0.001f && Mathf.Abs(b.y - terrain.Crossing.z) < 0.001f
                        && terrain.Crossing.x < a.x && terrain.Crossing.x > b.x)
                    {
                        Bank($"Bank {i} {sign} East", first,
                            new Vector2(terrain.Crossing.x + RouteTerrain.CrossingWidth * 0.5f + 0.15f, first.y));
                        Bank($"Bank {i} {sign} West",
                            new Vector2(terrain.Crossing.x - RouteTerrain.CrossingWidth * 0.5f - 0.15f, last.y), last);
                    }
                    else Bank($"Bank {i} {sign}", first, last);
                }
            }
            Bank("River End", terrain.RiverBankPoint(0, -1), terrain.RiverBankPoint(0, 1));
            RiverPieces.LayBanks(Root("Banks", parent), terrain);
            void Bank(string name, Vector2 a, Vector2 b) => Border(name, parent, terrain, a, b, 1.5f);
        }

        static void BuildLakeBanks(Transform parent, RouteTerrain terrain)
        {
            for (int i = 0; i < RouteTerrain.LakeSegments; i++)
                Border("Shore", parent, terrain, RouteTerrain.LakeShorePoint(i * Mathf.PI * 2f / RouteTerrain.LakeSegments),
                    RouteTerrain.LakeShorePoint((i + 1) * Mathf.PI * 2f / RouteTerrain.LakeSegments));
        }

        static void BuildRoad(Transform parent, RouteTerrain terrain, Material asphalt, Material stripe)
        {
            float start = terrain.Road.xMin;
            while (start < terrain.Road.xMax)
            {
                float end = Mathf.Min(start + 32f, terrain.Road.xMax);
                if (start < terrain.FenceStart.x) end = Mathf.Min(end, terrain.FenceStart.x);
                else if (start < terrain.RoadBendEnd) end = Mathf.Min(end, terrain.RoadBendEnd);
                float half = RouteTerrain.RoadWidth * 0.5f;
                var vertices = new[] { new Vector3(start, 0.04f, terrain.RoadCenterZ(start) - half),
                    new Vector3(start, 0.04f, terrain.RoadCenterZ(start) + half),
                    new Vector3(end, 0.04f, terrain.RoadCenterZ(end) - half),
                    new Vector3(end, 0.04f, terrain.RoadCenterZ(end) + half) };
                var mesh = SaveMesh($"Road {start}", vertices, new[] { 0, 1, 2, 2, 1, 3 },
                    new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one });
                var part = MeshObject("Asphalt", parent, mesh, asphalt);
                part.AddComponent<MeshCollider>().sharedMesh = mesh;
                part.AddComponent<GroundSurface>().type = GroundType.Slab;
                part.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
                start = end;
            }
            for (float x = terrain.Road.xMin + 5f; x < terrain.Road.xMax - 3f; x += 10f)
            {
                if (Mathf.Abs(x - RouteLayout.Clearing(6).x) < 18f) continue;
                var marking = SceneBuilder.CreatePrimitive("Marking", PrimitiveType.Cube, parent,
                    new Vector3(x, 0.05f, terrain.RoadCenterZ(x)), new Vector3(4f, 0.02f, 0.15f), stripe);
                float dz = terrain.RoadCenterZ(x + 0.1f) - terrain.RoadCenterZ(x - 0.1f);
                marking.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(dz, 0.2f) * Mathf.Rad2Deg, 0f);
                Object.DestroyImmediate(marking.GetComponent<Collider>());
            }
        }

        static void BuildTown(Transform parent, RouteTerrain terrain, Material material)
        {
            var building = GeneratedMaterials.Get("RouteBuilding", new Color(0.4f, 0.4f, 0.4f), occluder: true);
            var town = terrain.Town;
            var lots = new[] { new Vector2(town.xMin + 8f, town.yMin + 16f),
                new Vector2(town.xMin + 8f, town.yMin + 32f), new Vector2(town.xMin + 14f, town.yMax - 10f),
                new Vector2(town.xMin + 34f, town.yMax - 10f), new Vector2(town.xMax - 6f, town.yMax - 10f) };
            for (int i = 0; i < lots.Length; i++)
            {
                float x = lots[i].x, z = lots[i].y;
                float width = 6f + i % 3 * 2f, height = 4f + i % 4;
                var house = SceneBuilder.CreatePrimitive($"House {i + 1}", PrimitiveType.Cube, parent,
                    new Vector3(x, terrain.GroundHeight(x, z) + height * 0.5f, z), new Vector3(width, height, 8f), building);
                BlockNavigation(house);
            }
            var houseClearing = parent.parent.parent.GetChild(7);
            var sign = new GameObject("Tiffany Sign", typeof(RectTransform), typeof(Canvas)).transform;
            sign.SetParent(houseClearing, false);
            sign.localPosition = new Vector3(0f, 2.8f, -0.3f);
            sign.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var canvas = sign.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<UnityEngine.UI.Text>();
            label.transform.SetParent(sign, false);
            label.rectTransform.sizeDelta = new Vector2(300f, 50f);
            sign.localScale = Vector3.one * 0.02f;
            label.text = "Casa de Tiffany";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.color = Color.white;
        }

        static void BuildFence(Transform parent, RouteTerrain terrain, Material material)
        {
            RouteLayout.Gate(out var gate, out _, out float gateWidth);
            FenceRun("South", terrain.FenceStart.y, gate.z - gateWidth * 0.5f);
            FenceRun("North", gate.z + gateWidth * 0.5f, terrain.FenceEnd.y);
            var entrance = Root("Closed Gate", parent);
            entrance.localPosition = gate;
            var collider = entrance.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * 1.5f;
            collider.size = new Vector3(0.4f, 3f, gateWidth);
            BlockNavigation(entrance.gameObject);
            for (int i = 0; i <= 16; i++)
                Visual("Bar", entrance, new Vector3(0f, 1.5f, -gateWidth * 0.5f + i * 0.5f), new Vector3(0.12f, 3f, 0.12f));
            for (int i = 0; i < 2; i++) Visual("Rail", entrance, new Vector3(0f, 0.6f + i * 1.8f, 0f), new Vector3(0.15f, 0.15f, gateWidth));
            var town = terrain.Town;
            var house = RouteLayout.Clearing(7);
            var perimeter = new[] { new Vector2(town.xMin, town.yMin + 6f), new Vector2(house.x - 18f, town.yMin + 6f),
                new Vector2(house.x - 18f, house.z + 18f), new Vector2(house.x + 18f, house.z + 18f),
                new Vector2(house.x + 18f, town.yMin), new Vector2(town.xMax, town.yMin),
                town.max, new Vector2(town.xMin, town.yMax) };
            var boundary = Root("Town Boundary", parent);
            for (int i = 0; i < perimeter.Length; i++)
                FenceSection(boundary, terrain, perimeter[i], perimeter[(i + 1) % perimeter.Length], material);
            void FenceRun(string name, float start, float end)
            {
                if (end <= start) return;
                int count = Mathf.CeilToInt((end - start) / 4f);
                for (int i = 0; i <= count; i++)
                {
                    float z = Mathf.Lerp(start, end, (float)i / count);
                    float y = terrain.Height(gate.x, z);
                    Visual("Post", parent, new Vector3(gate.x, y + 1.5f, z), new Vector3(0.25f, 3f, 0.25f));
                    if (i == count) continue;
                    float next = Mathf.Lerp(start, end, (float)(i + 1) / count);
                    Border(name + " Collision", parent, terrain, new Vector2(gate.x, z), new Vector2(gate.x, next));
                    for (int rail = 0; rail < 2; rail++)
                        Visual("Rail", parent, new Vector3(gate.x, y + 0.6f + rail * 1.8f, (z + next) * 0.5f),
                            new Vector3(0.2f, 0.18f, next - z));
                }
            }
            void Visual(string name, Transform root, Vector3 position, Vector3 size) =>
                Object.DestroyImmediate(SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, root, position, size, material).GetComponent<Collider>());
        }

        static void FenceSection(Transform parent, RouteTerrain terrain, Vector2 a, Vector2 b, Material material)
        {
            int count = Mathf.CeilToInt(Vector2.Distance(a, b) / 8f);
            for (int i = 0; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(a, b, (float)i / count);
                float height = terrain.GroundHeight(point.x, point.y);
                Visual("Post", new Vector3(point.x, height + 1.5f, point.y), new Vector3(0.2f, 3f, 0.2f), Quaternion.identity);
                if (i == count) continue;
                Vector2 next = Vector2.Lerp(a, b, (float)(i + 1) / count);
                Border("Fence Collision", parent, terrain, point, next);
                Vector3 start = new Vector3(point.x, height, point.y);
                Vector3 end = new Vector3(next.x, terrain.GroundHeight(next.x, next.y), next.y);
                for (int rail = 0; rail < 2; rail++)
                    Visual("Rail", (start + end) * 0.5f + Vector3.up * (0.7f + rail * 1.6f),
                        new Vector3(0.16f, 0.16f, Vector3.Distance(start, end)), Quaternion.LookRotation(end - start));
            }
            void Visual(string name, Vector3 position, Vector3 size, Quaternion rotation)
            {
                var piece = SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, parent, position, size, material);
                piece.transform.localRotation = rotation;
                Object.DestroyImmediate(piece.GetComponent<Collider>());
            }
        }

        static void BuildRocks(Transform parent, RouteTerrain terrain, Material material)
        {
            var face = Root("Cliff Face", parent).gameObject;
            var wall = face.AddComponent<BoxCollider>();
            wall.center = new Vector3(75.75f, RouteTerrain.CliffHeight * 0.5f, 95f);
            wall.size = new Vector3(0.5f, RouteTerrain.CliffHeight, 10f);
            BlockNavigation(face);
            for (int i = 0; i < 7; i++)
            {
                string id = i % 2 == 0 ? "rock_largeA" : "stone_tallA";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Kenney/Nature/Models/{id}.fbx");
                if (asset == null) throw new System.InvalidOperationException($"Falta el modelo {id}.");
                var rock = Object.Instantiate(asset, parent, false);
                rock.name = id;
                var bounds = ModelLibrary.LocalBounds(rock.transform);
                float scale = (2.5f + i % 3) / bounds.size.y;
                rock.transform.localScale = Vector3.one * scale;
                float x = 74f + i % 2 * 2f, z = 88f + i * 2.2f;
                rock.transform.localPosition = new Vector3(x, terrain.GroundHeight(x, z) - bounds.min.y * scale - 0.5f, z);
                foreach (var renderer in rock.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                    renderer.sharedMaterials = materials;
                }
            }
        }
    }
}
