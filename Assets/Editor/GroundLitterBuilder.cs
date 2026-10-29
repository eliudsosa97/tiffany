using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class GroundLitterBuilder
    {
        public const float InteractionClearance = 1.2f;
        public const long AreaTriangleBudget = 60000;
        const float MinimumFloorWidth = 2.5f;
        const string WaterShader = "Universal Render Pipeline/Tiffany Water";

        public static void Scatter(Transform root)
        {
            Physics.SyncTransforms();
            int groundMask = ~LayerMask.GetMask(ForestLayout.CoverLayer, "Ignore Raycast");
            var floors = new HashSet<Collider>();
            var minimum = Vector3.positiveInfinity;
            var maximum = Vector3.negativeInfinity;
            float top = float.NegativeInfinity;
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || (groundMask & (1 << collider.gameObject.layer)) == 0) continue;
                var bounds = collider.bounds;
                top = Mathf.Max(top, bounds.max.y);
                if (!collider.TryGetComponent<GroundSurface>(out _) || !HasFloorFootprint(bounds)) continue;
                floors.Add(collider);
                minimum = Vector3.Min(minimum, bounds.min);
                maximum = Vector3.Max(maximum, bounds.max);
            }
            var exclusions = new List<Vector2>();
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.name == "Memory Point" || node.name == "Candle Point"
                    || node.TryGetComponent<MemorySpot>(out _) || node.TryGetComponent<CandleSpot>(out _)
                    || node.TryGetComponent<MovableObject>(out _))
                    exclusions.Add(new Vector2(node.position.x, node.position.z));
            var water = CollectWater(root);
            var meshes = GroundLitterMeshes.Load();
            var materials = Materials();
            var groups = new SortedDictionary<(int x, int z, int kind, int variant), List<Matrix4x4>>();
            var trianglesByGround = new long[Enum.GetValues(typeof(GroundType)).Length];
            var physics = root.gameObject.scene.GetPhysicsScene();
            var toLocal = root.worldToLocalMatrix;
            var tracker = root.GetComponentInChildren<WitchChapterTracker>();
            var terrain = tracker != null ? new RouteTerrain() : null;
            int noGround = 0, blocked = 0, steep = 0, reserved = 0, submerged = 0, absent = 0, distant = 0;
            var blockers = new Dictionary<string, int>();
            foreach (LitterKind kind in Enum.GetValues(typeof(LitterKind)))
            {
                if (floors.Count == 0) break;
                float size = GroundLitter.CellSize(kind);
                for (int z = Mathf.FloorToInt(minimum.z / size); z < Mathf.CeilToInt(maximum.z / size); z++)
                for (int x = Mathf.FloorToInt(minimum.x / size); x < Mathf.CeilToInt(maximum.x / size); x++)
                {
                    var candidate = GroundLitter.Sample(kind, x, z);
                    if (terrain != null)
                    {
                        Vector3 point = tracker.transform.InverseTransformPoint(candidate.Position);
                        if (terrain.Bounds.Contains(new Vector2(point.x, point.z)) && !terrain.InClearingSquare(point.x, point.z)
                            && terrain.DistanceToRoute(point.x, point.z) > 0f) { distant++; continue; }
                    }
                    var origin = new Vector3(candidate.Position.x, top + 1f, candidate.Position.z);
                    if (!physics.Raycast(origin, Vector3.down, out var hit, top - minimum.y + 2f,
                        groundMask, QueryTriggerInteraction.Ignore)) { noGround++; continue; }
                    if (!floors.Contains(hit.collider))
                    {
                        blocked++;
                        blockers.TryGetValue(hit.collider.name, out int count);
                        blockers[hit.collider.name] = count + 1;
                        continue;
                    }
                    if (hit.normal.y < 0.85f) { steep++; continue; }
                    var surface = candidate.Position;
                    surface.y = (float)Math.Round(hit.point.y, 4, MidpointRounding.ToEven);
                    var ground = hit.collider.GetComponent<GroundSurface>().type;
                    if (IsReserved(surface, exclusions)) { reserved++; continue; }
                    if (IsSubmerged(surface, water)) { submerged++; continue; }
                    if (!candidate.IsPresent(ground)) { absent++; continue; }
                    int variant = kind == LitterKind.Stone && ground == GroundType.Gravel ? GroundLitter.GravelVariant(candidate.Position) : candidate.Variant;
                    var mesh = meshes[(int)kind][variant];
                    float scale = candidate.Scale;
                    if (kind == LitterKind.Stone)
                        scale = ground == GroundType.Gravel ? Mathf.Lerp(0.9f, 1.1f, Mathf.InverseLerp(0.18f, 0.45f, scale))
                            : scale / Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
                    var position = surface;
                    if (kind != LitterKind.Root) position.y -= (mesh.bounds.min.y * scale + 0.002f) / hit.normal.y;
                    position.y = (float)Math.Round(position.y, 4, MidpointRounding.ToEven);
                    var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, candidate.Yaw, 0f);
                    var matrix = toLocal * Matrix4x4.TRS(position, rotation, Vector3.one * scale);
                    var chunk = GroundLitter.Chunk(candidate.Position);
                    var key = (chunk.x, chunk.y, (int)kind, variant);
                    if (!groups.TryGetValue(key, out var matrices)) groups.Add(key, matrices = new List<Matrix4x4>());
                    matrices.Add(matrix);
                    trianglesByGround[(int)ground] += mesh.GetIndexCount(0) / 3;
                }
            }
            var batches = new List<ForestInstanceData.Batch>();
            int instances = 0;
            long triangles = 0;
            foreach (var group in groups)
            {
                var mesh = meshes[group.Key.kind][group.Key.variant];
                var matrices = group.Value;
                var bounds = ForestInstances.TransformBounds(mesh.bounds, matrices[0]);
                for (int i = 1; i < matrices.Count; i++) bounds.Encapsulate(ForestInstances.TransformBounds(mesh.bounds, matrices[i]));
                batches.Add(new ForestInstanceData.Batch { mesh = mesh,
                    material = materials[group.Key.kind][group.Key.kind == (int)LitterKind.Leaves ? group.Key.variant : 0],
                    matrices = matrices.ToArray(), bounds = bounds });
                instances += matrices.Count;
                triangles += (long)mesh.GetIndexCount(0) / 3 * matrices.Count;
            }
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            string path = folder + "/GroundLitter.asset";
            var data = AssetDatabase.LoadAssetAtPath<ForestInstanceData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ForestInstanceData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.batches = batches.ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            var previous = root.Find("Ground Litter");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var litter = new GameObject("Ground Litter");
            litter.transform.SetParent(root, false);
            GameObjectUtility.SetStaticEditorFlags(litter, StaticEditorFlags.BatchingStatic);
            litter.AddComponent<ForestInstances>().data = data;
            string breakdown = "";
            foreach (GroundType ground in Enum.GetValues(typeof(GroundType)))
                breakdown += $" {ground}: {trianglesByGround[(int)ground]}.";
            Debug.Log($"{GameInfo.LogPrefix} Suelo: {instances} instancias, {triangles} triángulos, {batches.Count} lotes. Triángulos por suelo:{breakdown} "
                + $"Descartes: sin suelo: {noGround}, tapado por otro collider: {blocked} ({TopBlockers(blockers)}), "
                + $"pendiente: {steep}, reservado: {reserved}, bajo agua: {submerged}, ausente por densidad: {absent}, fuera de la ruta: {distant}.");
        }

        static string TopBlockers(Dictionary<string, int> blockers)
        {
            var sorted = new List<KeyValuePair<string, int>>(blockers);
            sorted.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var names = new List<string>();
            for (int i = 0; i < Mathf.Min(5, sorted.Count); i++) names.Add($"{sorted[i].Key}: {sorted[i].Value}");
            return names.Count == 0 ? "ninguno" : string.Join(", ", names);
        }

        static bool HasFloorFootprint(Bounds bounds) => Mathf.Min(bounds.size.x, bounds.size.z) >= MinimumFloorWidth;

        static bool IsReserved(Vector3 position, List<Vector2> exclusions)
        {
            var point = new Vector2(position.x, position.z);
            foreach (var exclusion in exclusions)
                if ((point - exclusion).sqrMagnitude <= InteractionClearance * InteractionClearance) return true;
            return false;
        }

        static Material[][] Materials()
        {
            var palette = LeafPalette();
            return new[]
            {
                new[] { Surface("LeavesRust", "FFFFFF", "dry_decay_leaves", true, palette),
                    Surface("LeavesOchre", "F2ECE0", "dry_decay_leaves", true, palette),
                    Surface("LeavesYellow", "EEE6CE", "dry_decay_leaves", true, palette) },
                new[] { Surface("Twig", "93806A", "pine_bark") },
                new[] { Surface("PineCone", "B98D60") },
                new[] { Surface("Stone", "918B7E", "rock_boulder_dry") },
                new[] { Surface("Root", "C5AD90", "jolcham_oak_bark_01") }
            };
        }

        static Texture2D LeafPalette()
        {
            GeneratedMaterials.EnsureFolder(GeneratedMaterials.folder);
            string path = GeneratedMaterials.folder + "/LitterLeafPalette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = palette == null;
            var shades = new[] { "9C603D", "7B583C", "A58243", "B0A05D", "C4B58E" };
            if (isNew) palette = new Texture2D(shades.Length, 1, TextureFormat.RGBA32, false) { name = "LitterLeafPalette" };
            palette.filterMode = FilterMode.Point;
            palette.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < shades.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + shades[i], out var color);
                palette.SetPixel(i, 0, color);
            }
            palette.Apply(false);
            if (isNew) AssetDatabase.CreateAsset(palette, path);
            else EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssetIfDirty(palette);
            return palette;
        }

        static Material Surface(string name, string tint, string texture = null, bool doubleSided = false, Texture2D palette = null)
        {
            ColorUtility.TryParseHtmlString("#" + tint, out var color);
            var material = texture == null ? GeneratedMaterials.Get("Litter" + name, color, instancing: true)
                : GeneratedMaterials.GetTextured("Litter" + name, color, texture, 1f, instancing: true);
            material.SetTexture("_BaseMap", palette);
            material.SetFloat("_BumpScale", 0.35f);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        sealed class WaterSurface
        {
            public Bounds bounds;
            public Vector3[] vertices;
            public int[] triangles;
        }

        static List<WaterSurface> CollectWater(Transform root)
        {
            var water = new List<WaterSurface>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent<Renderer>(out var renderer)) continue;
                var materials = renderer.sharedMaterials;
                var mesh = filter.sharedMesh;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (submesh >= materials.Length || materials[submesh] == null || materials[submesh].shader.name != WaterShader) continue;
                    var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
                    water.Add(new WaterSurface { bounds = renderer.bounds, vertices = vertices, triangles = mesh.GetTriangles(submesh) });
                }
            }
            return water;
        }

        static bool IsSubmerged(Vector3 point, List<WaterSurface> water)
        {
            foreach (var surface in water)
            {
                if (point.x < surface.bounds.min.x || point.x > surface.bounds.max.x
                    || point.z < surface.bounds.min.z || point.z > surface.bounds.max.z || point.y > surface.bounds.max.y + 0.02f) continue;
                var indices = surface.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = surface.vertices[indices[i]], b = surface.vertices[indices[i + 1]], c = surface.vertices[indices[i + 2]];
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < 0.000001f) continue;
                    float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / denominator;
                    float w = 1f - u - v;
                    if (u >= -0.00001f && v >= -0.00001f && w >= -0.00001f && point.y <= u * a.y + v * b.y + w * c.y + 0.02f)
                        return true;
                }
            }
            return false;
        }
    }
}
