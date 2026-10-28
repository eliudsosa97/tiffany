using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class ForestBuilder
    {
        public static void Populate(Transform clearing, int seed, float size, float pathWidth,
            (Vector2 center, float radius)[] exclusions, Vector2[] viewPoints = null)
        {
            int cover = LayerMask.NameToLayer(ForestLayout.CoverLayer);
            if (cover < 0) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta la capa Cover.");
            var previous = clearing.Find("Forest");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            int treeLimit = Mathf.Min(12, (200 - clearing.GetComponentsInChildren<Renderer>(true).Length) / 2);
            var forest = new GameObject("Forest").transform;
            forest.SetParent(clearing, false);
            var random = new System.Random(seed);
            var blocked = new List<(Vector2 center, float radius)>(exclusions);
            var localViewDirection = clearing.InverseTransformDirection(
                Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f) * Vector3.forward);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool Free(Vector3 position, float radius)
            {
                var p = new Vector2(position.x, position.z);
                if (Mathf.Abs(p.x) < pathWidth * 0.5f + radius + 0.5f
                    || Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) + radius > size * 0.5f - 0.5f) return false;
                foreach (var zone in blocked)
                    if ((p - zone.center).sqrMagnitude < (radius + zone.radius) * (radius + zone.radius)) return false;
                return true;
            }
            bool TryPosition(float radius, float height, out Vector3 position)
            {
                for (int attempt = 0; attempt < 2048; attempt++)
                {
                    position = new Vector3(Range(-size * 0.5f, size * 0.5f), 0f, Range(-size * 0.5f, size * 0.5f));
                    if (Free(position, radius) && KeepsView(position, radius, height, viewPoints, localViewDirection)) return true;
                }
                position = default;
                return false;
            }
            var oakBark = Surface("OakBark", new Color(0.7f, 0.65f, 0.58f), "jolcham_oak_bark_01");
            var pineBark = Surface("PineBark", new Color(0.65f, 0.61f, 0.53f), "pine_bark");
            var leaves = Surface("OakLeaves", new Color(0.7f, 0.65f, 0.43f), "dry_decay_leaves", true);
            var needles = Surface("PineNeedles", new Color(0.26f, 0.34f, 0.23f), "dry_decay_leaves", true);
            int trees = 0;
            long triangles = 0;
            for (int i = 0; i < treeLimit; i++)
            {
                bool pine = i % 3 == 2;
                var trunk = LoadMesh(pine ? "Ocote0" : "BugambiliasOak" + i % 3);
                var crown = LoadMesh(pine ? "OcoteCanopy0" : "BugambiliasLeaves" + i % 3);
                float height = Range(1.05f, 1.2f);
                var scale = pine ? Vector3.one * (height * 0.44f) : new Vector3(height * 0.7f, height, height * 0.7f);
                var bounds = trunk.bounds;
                bounds.Encapsulate(crown.bounds);
                if (!TryPosition(Radius(bounds) * scale.x, bounds.max.y * scale.y, out var position)) continue;
                var tree = Place(pine ? "Pine" : "Oak", forest, trunk, pine ? pineBark : oakBark);
                tree.transform.localPosition = position;
                tree.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                tree.transform.localScale = scale;
                var solid = tree.AddComponent<CapsuleCollider>();
                solid.radius = 0.65f;
                solid.height = crown.bounds.min.y;
                solid.center = Vector3.up * (solid.height * 0.5f);
                var canopy = Place("Canopy", tree.transform, crown, pine ? needles : leaves);
                canopy.layer = cover;
                var roof = canopy.AddComponent<BoxCollider>();
                roof.center = crown.bounds.center;
                roof.size = crown.bounds.size;
                blocked.Add((new Vector2(position.x, position.z), 0.9f));
                triangles += (trunk.GetIndexCount(0) + crown.GetIndexCount(0)) / 3;
                trees++;
            }
            var batches = new List<ForestInstanceData.Batch>();
            Scatter("GoldenGrass0", 128, Surface("Grass", new Color(0.42f, 0.46f, 0.27f), foliage: true));
            Scatter("DryShrub0", 32, Surface("Shrub", new Color(0.42f, 0.39f, 0.28f), foliage: true));
            Scatter("OakLitter0", 48, Surface("Litter", new Color(0.7f, 0.59f, 0.43f), "dry_decay_leaves", true));
            Scatter("GraniteStone0", 16, Surface("Stone", new Color(0.75f, 0.73f, 0.66f), "rock_boulder_dry"));
            void Scatter(string meshName, int count, Material material)
            {
                var mesh = LoadMesh(meshName);
                var matrices = new List<Matrix4x4>();
                var bounds = new Bounds();
                for (int i = 0; i < count; i++)
                {
                    float scale = Range(0.7f, 1.15f);
                    if (!TryPosition(Radius(mesh.bounds) * scale, 0f, out var position)) continue;
                    position.y = -mesh.bounds.min.y * scale;
                    var matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one * scale);
                    var pieceBounds = ForestInstances.TransformBounds(mesh.bounds, matrix);
                    if (matrices.Count == 0) bounds = pieceBounds;
                    else bounds.Encapsulate(pieceBounds);
                    matrices.Add(matrix);
                }
                batches.Add(new ForestInstanceData.Batch { mesh = mesh, material = material, bounds = bounds, matrices = matrices.ToArray() });
                triangles += mesh.GetIndexCount(0) / 3 * matrices.Count;
            }
            triangles += ForestGroundBuilder.AddTreePatches(forest, batches);
            string folder = Path.GetDirectoryName(GeneratedMaterials.folder) + "/Forest";
            GeneratedMaterials.EnsureFolder(folder);
            string path = $"{folder}/{clearing.name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<ForestInstanceData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ForestInstanceData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.batches = batches.ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            var understory = new GameObject("Understory");
            understory.transform.SetParent(forest, false);
            understory.AddComponent<ForestInstances>().data = data;
            foreach (var node in forest.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(node.gameObject, StaticEditorFlags.BatchingStatic);
            Debug.Log($"{GameInfo.LogPrefix} {clearing.name}: bosque con {trees * 2} renderers, {triangles} triángulos y hasta {trees * 2 + batches.Count} draws de color.");
        }

        static float Radius(Bounds bounds) => new Vector2(Mathf.Abs(bounds.center.x) + bounds.extents.x,
            Mathf.Abs(bounds.center.z) + bounds.extents.z).magnitude;

        static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Art/Stylized/Meshes/{name}.asset")
            ?? throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta la malla del bosque {name}.");

        static Material Surface(string name, Color color, string texture = null, bool foliage = false)
        {
            var material = texture == null ? GeneratedMaterials.Get("Forest" + name, color, instancing: true)
                : GeneratedMaterials.GetTextured("Forest" + name, color, texture, 1f, instancing: true);
            material.SetFloat("_Cull", foliage ? 0f : 2f);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Place(string name, Transform parent, Mesh mesh, Material material)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return piece;
        }
    }
}
