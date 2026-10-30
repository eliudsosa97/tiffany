using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static partial class PosterBuilder
    {
        public const string Folder = "Assets/Art/Posters";

        public static void Build(Transform route)
        {
            var previous = route.Find("Wanted Posters");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var materials = Materials();
            if (materials.Count == 0)
            {
                Debug.LogWarning($"{GameInfo.LogPrefix} No hay texturas wanted-poster en {Folder}. No se colocaron carteles.");
                return;
            }
            var terrain = new RouteTerrain();
            var candidates = CollectAnchors(route, terrain);
            var chosen = PosterLayout.Select(candidates, terrain);
            var root = new GameObject("Wanted Posters").transform;
            root.SetParent(route, false);
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic);
            var types = new int[3];
            var counts = new int[RouteLayout.Count];
            var visible = new int[RouteLayout.Count];
            for (int i = 0; i < chosen.Count; i++)
            {
                var anchor = chosen[i];
                var poster = GameObject.CreatePrimitive(PrimitiveType.Quad);
                poster.name = $"Wanted Poster {i + 1} {anchor.Kind}";
                Object.DestroyImmediate(poster.GetComponent<Collider>());
                poster.transform.SetParent(root, false);
                poster.transform.localPosition = anchor.Position;
                poster.transform.localRotation = anchor.Rotation;
                GameObjectUtility.SetStaticEditorFlags(poster, StaticEditorFlags.BatchingStatic);
                var renderer = poster.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = materials[i % materials.Count];
                var texture = renderer.sharedMaterial.mainTexture;
                poster.transform.localScale = new Vector3(PosterLayout.Height * texture.width / texture.height, PosterLayout.Height, 1f);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                types[(int)anchor.Kind]++;
                counts[PosterLayout.ClearingIndex(anchor.Position)]++;
                for (int c = 0; c < visible.Length; c++) if (PosterLayout.InView(anchor.Position, c)) visible[c]++;
            }
            BuildCaptureStop(root, terrain);
            Debug.Log($"{GameInfo.LogPrefix} Carteles: {chosen.Count}/{PosterLayout.MaxTotal}. Árbol: {types[0]}, poste de farol: {types[1]}, barda: {types[2]}.");
            for (int i = 0; i < counts.Length; i++)
                Debug.Log($"{GameInfo.LogPrefix} Claro {i + 1}: {counts[i]} carteles asignados, {visible[i]}/{PosterLayout.MaxPerClearing} en 80 x 80 m ({visible[i] * 2} triángulos).");
        }

        static void BuildCaptureStop(Transform root, RouteTerrain terrain)
        {
            if (root.childCount == 0) return;
            var poster = root.GetChild(0);
            float nearest = float.PositiveInfinity;
            foreach (Transform candidate in root)
            {
                Vector3 position = candidate.localPosition;
                float distance = RouteTerrain.RectangleDistance(new Vector2(position.x, position.z), terrain.Road);
                if (distance >= nearest) continue;
                nearest = distance;
                poster = candidate;
            }
            Vector3 facing = poster.localRotation * Vector3.back;
            Vector3 playerPosition = poster.localPosition + facing * 3f;
            playerPosition.y = terrain.GroundHeight(playerPosition.x, playerPosition.z);
            var stop = new GameObject("Poster 1 Near Road");
            stop.transform.SetParent(root, false);
            stop.transform.localPosition = playerPosition;
            stop.transform.localRotation = Quaternion.LookRotation(-facing);
            var capture = stop.AddComponent<CaptureStop>();
            capture.overrideCamera = true;
            capture.cameraPosition = root.TransformPoint(playerPosition + Vector3.up * 1.8f);
            capture.cameraRotation = Quaternion.LookRotation(poster.position - capture.cameraPosition);
            capture.wait = 1.5f;
            stop.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
        }

        static List<Material> Materials()
        {
            var materials = new List<Material>();
            for (int i = 1; i <= 3; i++)
            {
                string name = i == 1 ? "wanted-poster" : $"wanted-poster-{i}";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
                if (texture == null) continue;
                string path = $"{Folder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException($"{GameInfo.LogPrefix} Falta el shader URP/Lit para los carteles.");
                bool create = material == null;
                if (create) material = new Material(shader) { name = name };
                else material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_Metallic", 0f);
                if (create) AssetDatabase.CreateAsset(material, path);
                else EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                materials.Add(material);
            }
            return materials;
        }

        public static List<PosterLayout.Anchor> CollectAnchors(Transform route, RouteTerrain terrain)
        {
            var candidates = new List<PosterLayout.Anchor>();
            var surfaces = new Dictionary<Mesh, Surface>();
            var random = new System.Random(PosterLayout.Seed);
            var toRoute = route.worldToLocalMatrix;
            foreach (var filter in route.GetComponentsInChildren<MeshFilter>())
            {
                if (IsTrunk(filter.sharedMesh)) Add(filter.sharedMesh, toRoute * filter.transform.localToWorldMatrix, PosterLayout.AnchorKind.Tree);
            }
            foreach (var instances in route.GetComponentsInChildren<ForestInstances>())
            {
                if (instances.data == null) continue;
                foreach (var batch in instances.data.batches)
                {
                    if (!IsTrunk(batch.mesh)) continue;
                    foreach (var matrix in batch.matrices)
                        Add(batch.mesh, toRoute * instances.transform.localToWorldMatrix * matrix, PosterLayout.AnchorKind.Tree);
                }
            }
            foreach (var lamp in route.GetComponentsInChildren<StreetLamp>())
            {
                var pole = lamp.transform.Find("Pole");
                if (pole != null && pole.TryGetComponent<MeshFilter>(out var filter))
                    Add(filter.sharedMesh, toRoute * pole.localToWorldMatrix, PosterLayout.AnchorKind.Post);
            }
            var fence = route.Find("Landscape/Fence");
            if (fence != null)
                foreach (var filter in fence.GetComponentsInChildren<MeshFilter>())
                    if (filter.name == "Post") Add(filter.sharedMesh, toRoute * filter.transform.localToWorldMatrix, PosterLayout.AnchorKind.Fence);
            return candidates;

            void Add(Mesh mesh, Matrix4x4 matrix, PosterLayout.AnchorKind kind)
            {
                Vector3 position = matrix.MultiplyPoint3x4(Vector3.zero);
                if (mesh == null || !PosterLayout.Eligible(position, terrain)) return;
                Vector3 facing = PosterLayout.RouteDirection(position, terrain);
                float height = 1.4f + (float)random.NextDouble() * 0.3f;
                float yaw = (float)random.NextDouble() * 20f - 10f;
                float roll = (float)random.NextDouble() * 12f - 6f;
                position.y = terrain.GroundHeight(position.x, position.z) + height;
                var anchor = new PosterLayout.Anchor(position, facing, kind, yaw, roll);
                if (!surfaces.TryGetValue(mesh, out var surface))
                {
                    surface = new Surface(mesh);
                    surfaces.Add(mesh, surface);
                }
                if (!surface.TryAttach(matrix, anchor, out var attached) || !PosterLayout.Eligible(attached, terrain)) return;
                candidates.Add(new PosterLayout.Anchor(attached, facing, kind, yaw, roll));
            }
        }

        static bool IsTrunk(Mesh mesh) => mesh != null && (mesh.name == "Ocote0"
            || mesh.name.StartsWith("BugambiliasOak", System.StringComparison.Ordinal));
    }
}
