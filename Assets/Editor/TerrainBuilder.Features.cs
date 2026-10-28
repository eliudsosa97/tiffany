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
                    Vector2 first = BankPoint(i - 1, sign), last = BankPoint(i, sign);
                    if (Mathf.Abs(a.y - terrain.Crossing.z) < 0.001f && Mathf.Abs(b.y - terrain.Crossing.z) < 0.001f
                        && terrain.Crossing.x < a.x && terrain.Crossing.x > b.x)
                    {
                        Border($"Bank {i} {sign} East", parent, terrain, first,
                            new Vector2(terrain.Crossing.x + RouteTerrain.CrossingWidth * 0.5f + 0.15f, first.y));
                        Border($"Bank {i} {sign} West", parent, terrain,
                            new Vector2(terrain.Crossing.x - RouteTerrain.CrossingWidth * 0.5f - 0.15f, last.y), last);
                    }
                    else Border($"Bank {i} {sign}", parent, terrain, first, last);
                }
            }
            Vector2 BankPoint(int index, int sign)
            {
                var point = terrain.RiverPoint(index);
                var before = index == 0 ? terrain.RiverPoint(1) - point : point - terrain.RiverPoint(index - 1);
                var after = index == terrain.RiverPointCount - 1 ? before : terrain.RiverPoint(index + 1) - point;
                before = new Vector2(before.y, -before.x).normalized;
                after = new Vector2(after.y, -after.x).normalized;
                Vector2 normal = (before + after).normalized;
                return point + normal * (sign * RouteTerrain.RiverWidth * 0.5f / Vector2.Dot(normal, before));
            }
        }

        static void BuildLakeBanks(Transform parent, RouteTerrain terrain)
        {
            for (int i = 0; i < 96; i++) Border("Shore", parent, terrain, LakePoint(i), LakePoint(i + 1));
            Vector2 LakePoint(int index)
            {
                float angle = index * Mathf.PI * 2f / 96f;
                return RouteTerrain.LakeCenter + new Vector2(Mathf.Cos(angle) * RouteTerrain.LakeRadius.x,
                    Mathf.Sin(angle) * RouteTerrain.LakeRadius.y);
            }
        }

        static void BuildRoad(Transform parent, RouteTerrain terrain, Material asphalt, Material stripe)
        {
            float start = terrain.Road.xMin;
            while (start < terrain.Road.xMax)
            {
                float end = Mathf.Min(start + 32f, terrain.Road.xMax);
                var part = SceneBuilder.CreatePrimitive("Asphalt", PrimitiveType.Cube, parent,
                    new Vector3((start + end) * 0.5f, 0.02f, terrain.Road.center.y),
                    new Vector3(end - start, 0.04f, RouteTerrain.RoadWidth), asphalt);
                part.AddComponent<GroundSurface>().type = GroundType.Slab;
                part.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
                start = end;
            }
            for (float x = terrain.Road.xMin + 5f; x < terrain.Road.xMax - 3f; x += 10f)
            {
                if (Mathf.Abs(x - RouteLayout.Clearing(6).x) < 18f) continue;
                var marking = SceneBuilder.CreatePrimitive("Marking", PrimitiveType.Cube, parent,
                    new Vector3(x, 0.05f, terrain.Road.center.y), new Vector3(4f, 0.02f, 0.15f), stripe);
                Object.DestroyImmediate(marking.GetComponent<Collider>());
            }
        }

        static void BuildTown(Transform parent, RouteTerrain terrain, Material material)
        {
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 6; column++)
            {
                float x = -56f + column * 36f, z = 174f + row * 20f;
                if (terrain.InClearingSquare(x, z, 8f) || terrain.DistanceToRoute(x, z) < 8f) continue;
                float width = 6f + (column + row) % 3 * 2f, height = 4f + (column * 3 + row) % 4;
                var house = SceneBuilder.CreatePrimitive($"House {row} {column}", PrimitiveType.Cube, parent,
                    new Vector3(x, terrain.GroundHeight(x, z) + height * 0.5f, z), new Vector3(width, height, 8f), material);
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
            void FenceRun(string name, float start, float end)
            {
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

        static void BuildRocks(Transform parent, RouteTerrain terrain, Material material)
        {
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
