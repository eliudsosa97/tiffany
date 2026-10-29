using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class ForestBuilder
    {
        public const float CellSize = 32f;
        public const float FillTreeSpacing = 4f;
        public const float UnderstoryDistance = 10f;
        public const long AreaTriangleBudget = 250000;

        public static void FillArea(Transform parent, RouteTerrain terrain)
        {
            int cover = LayerMask.NameToLayer(ForestLayout.CoverLayer);
            if (cover < 0) throw new InvalidOperationException("Falta la capa Cover.");
            var previous = parent.Find("Forest Fill");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject("Forest Fill").transform;
            root.SetParent(parent, false);
            var ids = new[] { "BugambiliasOak0", "BugambiliasLeaves0", "BugambiliasOak1", "BugambiliasLeaves1",
                "Ocote0", "OcoteCanopy0", "GoldenGrass0", "DryShrub0", "OakLitter0" };
            var meshes = new Mesh[ids.Length];
            for (int i = 0; i < ids.Length; i++) meshes[i] = LoadMesh(ids[i]);
            var oak = Surface("OakBark", new Color(0.7f, 0.65f, 0.58f), "jolcham_oak_bark_01");
            var leaves = Surface("OakLeaves", new Color(0.7f, 0.65f, 0.43f), "dry_decay_leaves", true);
            var materials = new[] { oak, leaves, oak, leaves,
                Surface("PineBark", new Color(0.65f, 0.61f, 0.53f), "pine_bark"),
                Surface("PineNeedles", new Color(0.26f, 0.34f, 0.23f), "dry_decay_leaves", true),
                Surface("Grass", new Color(0.42f, 0.46f, 0.27f), foliage: true),
                Surface("Shrub", new Color(0.42f, 0.39f, 0.28f), foliage: true),
                Surface("Litter", new Color(0.7f, 0.59f, 0.43f), "dry_decay_leaves", true) };
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
            var used = new long[RouteLayout.Count];
            for (int i = 0; i < used.Length; i++) used[i] = TriangleCount(parent.parent.GetChild(i).Find("Forest"));
            var cells = new SortedDictionary<Vector2Int, FillCell>(Comparer<Vector2Int>.Create((a, b) =>
                a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)));
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool Free(Vector2 point, float radius) => terrain.Bounds.Contains(point)
                && terrain.DistanceToRoute(point.x, point.y) > radius + 0.5f
                && !terrain.InClearingSquare(point.x, point.y, radius)
                && RouteTerrain.RectangleDistance(point, terrain.Town) > radius
                && terrain.LakeDistance(point.x, point.y) > radius + 1f
                && terrain.RiverDistance(point.x, point.y) > RouteTerrain.RiverWidth * 0.5f + radius
                && terrain.TrainDistance(point.x, point.y) > RouteLayout.TrainWidth * 0.5f + radius + 0.5f;
            for (float z = terrain.Bounds.yMin + 3f; z < terrain.Bounds.yMax - 3f; z += FillTreeSpacing)
            for (float x = terrain.Bounds.xMin + 3f; x < terrain.Bounds.xMax - 3f; x += FillTreeSpacing)
            {
                var point = new Vector2(x + Range(-1f, 1f), z + Range(-1f, 1f));
                if (random.NextDouble() >= terrain.ForestDensity(point.x, point.y)) continue;
                bool haunted = terrain.IsHauntedForest(point.x, point.y);
                int model = haunted ? (random.Next(4) == 0 ? 2 : 4) : random.Next(3) * 2;
                float height = haunted ? Range(1.25f, 1.4f) : Range(1.05f, 1.2f);
                float width = height * (haunted ? 0.85f : 0.7f);
                var scale = model == 4 ? Vector3.one * (height * 0.44f) : new Vector3(width, height, width);
                var bounds = meshes[model].bounds;
                bounds.Encapsulate(meshes[model + 1].bounds);
                float radius = Radius(bounds) * scale.x;
                if (!Free(point, radius) || !KeepsView(new Vector3(point.x, 0f, point.y), radius,
                    bounds.max.y * scale.y, viewPoints, viewDirection)) continue;
                float ground = terrain.GroundHeight(point.x, point.y);
                if (Mathf.Abs(terrain.GroundHeight(point.x - radius, point.y) - ground) > 1f
                    || Mathf.Abs(terrain.GroundHeight(point.x + radius, point.y) - ground) > 1f
                    || Mathf.Abs(terrain.GroundHeight(point.x, point.y - radius) - ground) > 1f
                    || Mathf.Abs(terrain.GroundHeight(point.x, point.y + radius) - ground) > 1f) continue;
                var cell = Reserve(point, Triangles(meshes[model]) + Triangles(meshes[model + 1]));
                if (cell == null) continue;
                Vector3 position = new Vector3(point.x, ground, point.y) - cell.root.localPosition;
                var matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, Range(0f, 360f), 0f), scale);
                cell.groups[model].Add(matrix);
                cell.groups[model + 1].Add(matrix);
                var trunk = cell.root.gameObject.AddComponent<CapsuleCollider>();
                trunk.radius = 0.65f * scale.x;
                trunk.height = meshes[model + 1].bounds.min.y * scale.y;
                trunk.center = position + Vector3.up * (trunk.height * 0.5f);
                var crown = cell.canopies.gameObject.AddComponent<BoxCollider>();
                var crownBounds = ForestInstances.TransformBounds(meshes[model + 1].bounds, matrix);
                Vector3 crownWorld = cell.root.localPosition + crownBounds.center;
                float bottom = Mathf.Max(crownBounds.min.y, terrain.GroundHeight(crownWorld.x, crownWorld.z) + 2.6f);
                crownBounds.SetMinMax(new Vector3(crownBounds.min.x, bottom, crownBounds.min.z),
                    new Vector3(crownBounds.max.x, Mathf.Max(bottom + 0.2f, crownBounds.max.y), crownBounds.max.z));
                crown.center = crownBounds.center;
                crown.size = crownBounds.size;
            }
            for (float z = terrain.Bounds.yMin + 2f; z < terrain.Bounds.yMax - 2f; z += 4f)
            for (float x = terrain.Bounds.xMin + 2f; x < terrain.Bounds.xMax - 2f; x += 4f)
            {
                var point = new Vector2(x + Range(-1f, 1f), z + Range(-1f, 1f));
                if (terrain.DistanceToRoute(point.x, point.y, false) > UnderstoryDistance || random.NextDouble() > 0.5) continue;
                int model = 6 + random.Next(3);
                float scale = Range(0.7f, 1.15f);
                if (!Free(point, Radius(meshes[model].bounds) * scale)) continue;
                var cell = Reserve(point, Triangles(meshes[model]));
                if (cell == null) continue;
                Vector3 position = new Vector3(point.x, terrain.GroundHeight(point.x, point.y) - meshes[model].bounds.min.y * scale,
                    point.y) - cell.root.localPosition;
                cell.groups[model].Add(Matrix4x4.TRS(position, Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one * scale));
            }
            FillCell Reserve(Vector2 point, long triangles)
            {
                var key = new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
                var bounds = CellBounds(key);
                for (int i = 0; i < used.Length; i++)
                    if (bounds.Overlaps(ViewSquare(i)) && used[i] + triangles > AreaTriangleBudget) return null;
                for (int i = 0; i < used.Length; i++)
                    if (bounds.Overlaps(ViewSquare(i))) used[i] += triangles;
                if (cells.TryGetValue(key, out var existing)) return existing;
                var cell = new FillCell { root = new GameObject($"Fill {key.x} {key.y}").transform,
                    canopies = new GameObject("Canopies").transform, groups = new List<Matrix4x4>[meshes.Length] };
                cell.root.SetParent(root, false);
                cell.root.localPosition = new Vector3(key.x * CellSize, 0f, key.y * CellSize);
                var modifier = cell.root.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = NavMesh.GetAreaFromName("Not Walkable");
                cell.canopies.SetParent(cell.root, false);
                cell.canopies.gameObject.layer = cover;
                cell.canopies.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                for (int i = 0; i < cell.groups.Length; i++) cell.groups[i] = new List<Matrix4x4>();
                cells.Add(key, cell);
                return cell;
            }
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            foreach (var cell in cells.Values)
            {
                string path = $"{folder}/{cell.root.name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<ForestInstanceData>(path);
                if (data == null) { data = ScriptableObject.CreateInstance<ForestInstanceData>(); AssetDatabase.CreateAsset(data, path); }
                var batches = new List<ForestInstanceData.Batch>();
                for (int i = 0; i < meshes.Length; i++)
                {
                    var matrices = cell.groups[i];
                    if (matrices.Count == 0) continue;
                    var bounds = ForestInstances.TransformBounds(meshes[i].bounds, matrices[0]);
                    foreach (var matrix in matrices) bounds.Encapsulate(ForestInstances.TransformBounds(meshes[i].bounds, matrix));
                    bounds.Expand(0.01f);
                    batches.Add(new ForestInstanceData.Batch { mesh = meshes[i], material = materials[i],
                        matrices = matrices.ToArray(), bounds = bounds });
                }
                data.batches = batches.ToArray();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                cell.root.gameObject.AddComponent<ForestInstances>().data = data;
            }
            for (int i = 0; i < used.Length; i++) Debug.Log($"{GameInfo.LogPrefix} Claro {i}: bosque y relleno <= {used[i]} triángulos en 80 x 80 m.");
        }

        sealed class FillCell
        {
            public Transform root, canopies;
            public List<Matrix4x4>[] groups;
        }

        public static Rect CellBounds(Vector2Int cell) =>
            new Rect(cell.x * CellSize - 8f, cell.y * CellSize - 8f, CellSize + 16f, CellSize + 16f);

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
