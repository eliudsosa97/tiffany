using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static partial class TerrainBuilder
    {
        public const float GridSize = 2f;
        public const float TileSize = 64f;
        public static string Folder => Path.GetDirectoryName(GeneratedMaterials.folder) + "/Terrain";

        public static Transform Build(Transform route, RouteTerrain terrain, Material ground, Material structure, Material dark)
        {
            var landscape = Root("Landscape", route);
            var tiles = Root("Terrain", landscape);
            for (float z = terrain.Bounds.yMin; z < terrain.Bounds.yMax; z += TileSize)
            for (float x = terrain.Bounds.xMin; x < terrain.Bounds.xMax; x += TileSize)
                BuildTile(tiles, terrain, Rect.MinMaxRect(x, z, Mathf.Min(x + TileSize, terrain.Bounds.xMax),
                    Mathf.Min(z + TileSize, terrain.Bounds.yMax)), ground);
            BuildBoundary(Root("Walkable Boundary", landscape), terrain);
            BuildRiver(Root("River", landscape), terrain);
            BuildLakeBanks(Root("Lake Banks", landscape), terrain);
            BuildRoad(Root("Road", landscape), terrain, dark, structure);
            BuildTown(Root("Town", landscape), terrain, structure);
            BuildFence(Root("Fence", landscape), terrain, structure);
            BuildRocks(Root("Cliff Rocks", landscape), terrain, structure);
            ForestBuilder.FillArea(landscape, terrain, terrain.IsWalkable, dark);
            return landscape;
        }

        static void BuildTile(Transform parent, RouteTerrain terrain, Rect rectangle, Material material)
        {
            var xs = Grid(rectangle.xMin, rectangle.xMax, 75.5f, 76f);
            var zs = Grid(rectangle.yMin, rectangle.yMax, terrain.Crossing.z - 2f, terrain.Crossing.z + 2f);
            var vertices = new Vector3[xs.Count * zs.Count];
            var uv = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (int z = 0; z < zs.Count; z++)
            for (int x = 0; x < xs.Count; x++)
            {
                int index = z * xs.Count + x;
                vertices[index] = new Vector3(xs[x], terrain.GroundHeight(xs[x], zs[z]), zs[z]);
                uv[index] = new Vector2(xs[x], zs[z]) * 0.1f;
                normals[index] = new Vector3(terrain.GroundHeight(xs[x] - 0.1f, zs[z]) - terrain.GroundHeight(xs[x] + 0.1f, zs[z]),
                    0.2f, terrain.GroundHeight(xs[x], zs[z] - 0.1f) - terrain.GroundHeight(xs[x], zs[z] + 0.1f)).normalized;
            }
            var walkable = new List<int>();
            var wild = new List<int>();
            var bed = new List<int>();
            for (int z = 0; z < zs.Count - 1; z++)
            for (int x = 0; x < xs.Count - 1; x++)
            {
                int a = z * xs.Count + x, b = a + xs.Count;
                Triangle(a, b, a + 1);
                Triangle(a + 1, b, b + 1);
            }
            void Triangle(int a, int b, int c)
            {
                Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                var indices = terrain.IsWater(center.x, center.z) ? bed
                    : terrain.WalkableDistance(center.x, center.z) < 0.5f ? walkable : wild;
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
            string name = $"Tile {rectangle.xMin} {rectangle.yMin}";
            Make("Ground", walkable, false);
            Make("Wild", wild, true);
            Make("Bed", bed, false);
            void Make(string suffix, List<int> indices, bool blocked)
            {
                if (indices.Count == 0) return;
                var mesh = SaveMesh(name + " " + suffix, vertices, indices.ToArray(), uv, normals);
                var piece = MeshObject(name + " " + suffix, parent, mesh, material);
                piece.AddComponent<MeshCollider>().sharedMesh = mesh;
                piece.AddComponent<GroundSurface>().type = GroundType.Dirt;
                if (blocked) BlockNavigation(piece);
                if (suffix == "Bed") piece.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
        }

        static List<float> Grid(float min, float max, params float[] extra)
        {
            var values = new List<float>();
            for (float value = min; value < max; value += GridSize) values.Add(value);
            values.Add(max);
            foreach (float value in extra)
                if (value > min && value < max && !values.Contains(value)) values.Add(value);
            values.Sort();
            return values;
        }

        public static void PathFloor(Transform parent, string name, Vector3 a, Vector3 b, Vector3 origin,
            RouteTerrain terrain, Material material)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / GridSize);
            var side = Vector3.Cross(Vector3.up, b - a).normalized * (RouteLayout.PathWidth * 0.5f);
            var vertices = new Vector3[(steps + 1) * 3];
            var indices = new List<int>();
            for (int i = 0; i <= steps; i++)
            for (int lane = 0; lane < 3; lane++)
            {
                Vector3 point = Vector3.Lerp(a, b, (float)i / steps) + side * (lane - 1);
                point.y = terrain.Height(point.x, point.z) + 0.01f;
                vertices[i * 3 + lane] = point - origin;
                if (i == steps || lane == 2) continue;
                int index = i * 3 + lane;
                indices.AddRange(new[] { index, index + 3, index + 1, index + 1, index + 3, index + 4 });
            }
            var mesh = SaveMesh(parent.parent.name + " " + name, vertices, indices.ToArray());
            var floor = MeshObject(name, parent, mesh, material);
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            floor.AddComponent<GroundSurface>().type = GroundType.Dirt;
            HideFloor(floor);
        }

        public static void HideFloor(GameObject floor)
        {
            floor.GetComponent<Renderer>().enabled = false;
            var modifier = floor.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = false;
        }

        static void BuildBoundary(Transform parent, RouteTerrain terrain)
        {
            const float step = 1f;
            int width = Mathf.CeilToInt(terrain.Bounds.width) + 3;
            int depth = Mathf.CeilToInt(terrain.Bounds.height) + 3;
            var values = new float[width * depth];
            var start = terrain.Bounds.min - Vector2.one;
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++) values[z * width + x] = terrain.WalkableDistance(start.x + x, start.y + z);
            for (int z = 0; z < depth - 1; z++)
            for (int x = 0; x < width - 1; x++)
            {
                Vector2 a = start + new Vector2(x, z) * step;
                int index = z * width + x;
                Contour(a, a + Vector2.up, a + Vector2.one, values[index], values[index + width], values[index + width + 1]);
                Contour(a, a + Vector2.one, a + Vector2.right, values[index], values[index + width + 1], values[index + 1]);
            }
            void Contour(Vector2 a, Vector2 b, Vector2 c, float da, float db, float dc)
            {
                Vector2 first = default, second = default;
                int hits = 0;
                Edge(a, b, da, db); Edge(b, c, db, dc); Edge(c, a, dc, da);
                if (hits == 2 && Vector2.Distance(first, second) > 0.01f) Border("Boundary", parent, terrain, first, second);
                void Edge(Vector2 p, Vector2 q, float dp, float dq)
                {
                    if ((dp <= 0f) == (dq <= 0f)) return;
                    Vector2 hit = Vector2.Lerp(p, q, dp / (dp - dq));
                    if (hits++ == 0) first = hit; else second = hit;
                }
            }
        }

        static void Border(string name, Transform parent, RouteTerrain terrain, Vector2 a, Vector2 b)
        {
            Vector3 start = new Vector3(a.x, terrain.Height(a.x, a.y), a.y);
            Vector3 end = new Vector3(b.x, terrain.Height(b.x, b.y), b.y);
            float low = Mathf.Min(start.y, end.y) - 0.4f;
            float height = Mathf.Abs(start.y - end.y) + 3.4f;
            var middle = (start + end) * 0.5f;
            middle.y = low + height * 0.5f;
            SceneBuilder.CreateBorder(name, parent, parent.TransformPoint(middle),
                parent.rotation * Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.y - a.y)),
                new Vector3(0.18f, height, Vector2.Distance(a, b) + 0.05f));
            BlockNavigation(parent.GetChild(parent.childCount - 1).gameObject);
        }

        internal static Mesh SaveMesh(string name, Vector3[] vertices, int[] triangles, Vector2[] uv = null, Vector3[] normals = null)
        {
            GeneratedMaterials.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            else mesh.Clear();
            mesh.indexFormat = vertices.Length <= ushort.MaxValue ? IndexFormat.UInt16 : IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            if (uv != null) mesh.uv = uv;
            if (normals == null) mesh.RecalculateNormals();
            else mesh.normals = normals;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }

        internal static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var piece = Root(name, parent).gameObject;
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return piece;
        }

        static void BlockNavigation(GameObject piece)
        {
            var modifier = piece.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        static Transform Root(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }
    }
}
