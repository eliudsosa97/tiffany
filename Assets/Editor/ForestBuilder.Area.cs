using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class ForestBuilder
    {
        public const float CellSize = 32f;
        public const long AreaTriangleBudget = 140000;

        public static void FillArea(Transform parent, RouteTerrain terrain, Func<float, float, bool> walkable, Material material)
        {
            var previous = parent.Find("Forest Fill");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject("Forest Fill").transform;
            root.SetParent(parent, false);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            var ids = new[] { "tree_pineRoundC", "tree_pineTallA_detailed", "tree_pineTallC_detailed" };
            var meshes = new Mesh[ids.Length];
            for (int i = 0; i < meshes.Length; i++) meshes[i] = AreaMesh(ids[i]);
            var random = new System.Random(20517389);
            var views = new List<Vector2>();
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector3 center = RouteLayout.Clearing(i);
                foreach (var point in RouteViewPoints(i)) views.Add(point + new Vector2(center.x, center.z));
            }
            var viewPoints = views.ToArray();
            var viewDirection = parent.InverseTransformDirection(
                Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f) * Vector3.forward);
            var candidates = new List<(Vector2 point, float distance, int model, float scale, float yaw)>();
            for (float z = terrain.Bounds.yMin + 2f; z < terrain.Bounds.yMax - 2f; z += 3f)
            for (float x = terrain.Bounds.xMin + 2f; x < terrain.Bounds.xMax - 2f; x += 3f)
            {
                var point = new Vector2(x + (float)random.NextDouble(), z + (float)random.NextDouble());
                float distance = terrain.DistanceToRoute(point.x, point.y);
                if (distance < 1.4f || distance > 20f || walkable(point.x, point.y)
                    || terrain.InClearingSquare(point.x, point.y, 1f) || terrain.Town.Contains(point)
                    || terrain.LakeDistance(point.x, point.y) < 2f || terrain.RiverDistance(point.x, point.y) < 4f) continue;
                if (distance > 9f && random.NextDouble() > 0.35) continue;
                int model = random.Next(meshes.Length);
                float scale = 0.8f + (float)random.NextDouble() * 0.35f;
                if (!KeepsView(new Vector3(point.x, 0f, point.y), Radius(meshes[model].bounds) * scale,
                    meshes[model].bounds.max.y * scale, viewPoints, viewDirection)) continue;
                candidates.Add((point, distance, model, scale, (float)random.NextDouble() * 360f));
            }
            candidates.Sort((a, b) =>
            {
                int distance = a.distance.CompareTo(b.distance);
                if (distance != 0) return distance;
                int z = a.point.y.CompareTo(b.point.y);
                return z != 0 ? z : a.point.x.CompareTo(b.point.x);
            });
            var used = new long[RouteLayout.Count];
            for (int i = 0; i < used.Length; i++) used[i] = TriangleCount(parent.parent.GetChild(i).Find("Forest"));
            var cells = new SortedDictionary<Vector2Int, List<Matrix4x4>[]>(Comparer<Vector2Int>.Create((a, b) =>
                a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)));
            foreach (var candidate in candidates)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(candidate.point.x / CellSize), Mathf.FloorToInt(candidate.point.y / CellSize));
                var bounds = new Rect(cell.x * CellSize - 8f, cell.y * CellSize - 8f, CellSize + 16f, CellSize + 16f);
                long triangles = Triangles(meshes[candidate.model]);
                bool fits = true;
                for (int i = 0; i < used.Length; i++)
                    if (bounds.Overlaps(ViewSquare(i)) && used[i] + triangles > AreaTriangleBudget) fits = false;
                if (!fits) continue;
                if (!cells.TryGetValue(cell, out var groups))
                {
                    groups = new List<Matrix4x4>[meshes.Length];
                    for (int i = 0; i < groups.Length; i++) groups[i] = new List<Matrix4x4>();
                    cells.Add(cell, groups);
                }
                for (int i = 0; i < used.Length; i++)
                    if (bounds.Overlaps(ViewSquare(i))) used[i] += triangles;
                Vector3 position = new Vector3(candidate.point.x - cell.x * CellSize,
                    terrain.GroundHeight(candidate.point.x, candidate.point.y), candidate.point.y - cell.y * CellSize);
                groups[candidate.model].Add(Matrix4x4.TRS(position, Quaternion.Euler(0f, candidate.yaw, 0f), Vector3.one * candidate.scale));
            }
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            foreach (var cell in cells)
            {
                string name = $"Fill {cell.Key.x} {cell.Key.y}";
                string path = $"{folder}/{name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<ForestInstanceData>(path);
                if (data == null) { data = ScriptableObject.CreateInstance<ForestInstanceData>(); AssetDatabase.CreateAsset(data, path); }
                var batches = new List<ForestInstanceData.Batch>();
                for (int i = 0; i < meshes.Length; i++)
                {
                    var matrices = cell.Value[i];
                    if (matrices.Count == 0) continue;
                    var bounds = ForestInstances.TransformBounds(meshes[i].bounds, matrices[0]);
                    foreach (var matrix in matrices) bounds.Encapsulate(ForestInstances.TransformBounds(meshes[i].bounds, matrix));
                    batches.Add(new ForestInstanceData.Batch { mesh = meshes[i], material = material,
                        matrices = matrices.ToArray(), bounds = bounds });
                }
                data.batches = batches.ToArray();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                var instance = new GameObject(name).AddComponent<ForestInstances>();
                instance.transform.SetParent(root, false);
                instance.transform.localPosition = new Vector3(cell.Key.x * CellSize, 0f, cell.Key.y * CellSize);
                instance.data = data;
            }
            for (int i = 0; i < used.Length; i++) Debug.Log($"{GameInfo.LogPrefix} Claro {i}: bosque y relleno <= {used[i]} triángulos en 80 x 80 m.");
        }

        static Mesh AreaMesh(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Kenney/Nature/Models/{id}.fbx");
            if (asset == null) throw new InvalidOperationException($"Falta el modelo {id}.");
            var combine = new List<CombineInstance>();
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                combine.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = submesh,
                    transform = asset.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            var combined = new Mesh();
            try
            {
                combined.CombineMeshes(combine.ToArray(), true, true);
                var bounds = combined.bounds;
                float scale = 6.5f / bounds.size.y;
                Vector3 basePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var vertices = combined.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] - basePoint) * scale;
                return TerrainBuilder.SaveMesh("Forest " + id, vertices, combined.triangles);
            }
            finally { Object.DestroyImmediate(combined); }
        }

        public static Rect ViewSquare(int clearing)
        {
            var center = RouteLayout.Clearing(clearing);
            return new Rect(center.x - 40f, center.z - 40f, 80f, 80f);
        }

        public static long TriangleCount(Transform root)
        {
            if (root == null) return 0;
            long count = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) count += Triangles(filter.sharedMesh);
            foreach (var instances in root.GetComponentsInChildren<ForestInstances>())
                foreach (var batch in instances.data.batches) count += Triangles(batch.mesh) * batch.matrices.Length;
            return count;
        }

        static long Triangles(Mesh mesh)
        {
            long triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) triangles += mesh.GetIndexCount(i) / 3;
            return triangles;
        }
    }
}
