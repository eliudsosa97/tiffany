using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class ForestGroundBuilder
    {
        const int NeedleCount = 96;

        public static long AddTreePatches(Transform forest, List<ForestInstanceData.Batch> batches)
        {
            var oaks = new List<Matrix4x4>();
            var pines = new List<Matrix4x4>();
            foreach (Transform tree in forest)
            {
                if (tree.name != "Oak" && tree.name != "Pine") continue;
                var bounds = tree.GetComponent<MeshFilter>().sharedMesh.bounds;
                bounds.Encapsulate(tree.Find("Canopy").GetComponent<MeshFilter>().sharedMesh.bounds);
                float footprint = new Vector2(Mathf.Abs(bounds.center.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.z) + bounds.extents.z).magnitude * tree.localScale.x;
                float diameter = Mathf.Min(4.8f, footprint);
                var matrix = Matrix4x4.TRS(tree.localPosition + Vector3.up * 0.012f, tree.localRotation,
                    new Vector3(diameter, 1f, diameter));
                (tree.name == "Pine" ? pines : oaks).Add(matrix);
            }
            long triangles = 0;
            if (oaks.Count > 0)
                Add(PatchMesh(false), GroundMaterials.TreePatch("ForestGroundOak", "dry_decay_leaves", "A58B6B", "dry_ground_rocks", 0.20f), oaks);
            if (pines.Count > 0)
            {
                Add(PatchMesh(false), GroundMaterials.TreePatch("ForestGroundPine", "dry_ground_rocks", "91816A", "burned_ground_01", 0.25f), pines);
                var needles = GroundMaterials.TreePatch("ForestGroundNeedles", "pine_bark", "C2A779", "pine_bark", 0f);
                needles.renderQueue++;
                EditorUtility.SetDirty(needles);
                Add(PatchMesh(true), needles, pines);
            }
            return triangles;

            void Add(Mesh mesh, Material material, List<Matrix4x4> matrices)
            {
                var bounds = ForestInstances.TransformBounds(mesh.bounds, matrices[0]);
                for (int i = 1; i < matrices.Count; i++) bounds.Encapsulate(ForestInstances.TransformBounds(mesh.bounds, matrices[i]));
                batches.Add(new ForestInstanceData.Batch { mesh = mesh, material = material, matrices = matrices.ToArray(), bounds = bounds });
                triangles += mesh.GetIndexCount(0) / 3 * matrices.Count;
            }
        }

        static Mesh PatchMesh(bool needles)
        {
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            string name = needles ? "PineNeedlePatch" : "ForestFloorPatch";
            string path = $"{folder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var uv = new List<Vector2>();
            void Quad(Vector3 center, Vector3 along, Vector3 across)
            {
                int start = vertices.Count;
                vertices.Add(center - along - across);
                vertices.Add(center + along - across);
                vertices.Add(center + along + across);
                vertices.Add(center - along + across);
                for (int i = start; i < start + 4; i++) uv.Add(new Vector2(vertices[i].x + 0.5f, vertices[i].z + 0.5f));
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            if (!needles) Quad(Vector3.zero, Vector3.forward * 0.5f, Vector3.right * 0.5f);
            else
            {
                var random = new System.Random(17441);
                float Next() => (float)random.NextDouble();
                for (int i = 0; i < NeedleCount; i++)
                {
                    float angle = Next() * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Next()) * 0.40f;
                    var center = new Vector3(Mathf.Cos(angle) * radius, 0.004f + i * 0.00001f, Mathf.Sin(angle) * radius);
                    var rotation = Quaternion.Euler(0f, Next() * 360f, 0f);
                    Quad(center, rotation * Vector3.forward * (0.018f + Next() * 0.025f), rotation * Vector3.right * 0.0015f);
                }
            }
            mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
