using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class CharacterSilhouetteBuilder
    {
        public static int Add(Transform figure)
        {
            if (figure.GetComponentInParent<WitchController>(true) != null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} La bruja no lleva silueta.");
            var shader = Shader.Find(GeneratedMaterials.OcclusionShader);
            if (shader == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el shader de visibilidad.");
            var silhouette = GeneratedMaterials.GetSilhouette();
            var renderers = figure.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            foreach (var renderer in renderers)
            {
                MeshFilter filter = null;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin == null && (!(renderer is MeshRenderer) || !renderer.TryGetComponent(out filter))) continue;
                if (renderer.GetComponentInParent<ParticleSystem>(true) != null
                    || renderer.GetComponentInParent<Light>(true) != null) continue;
                var mesh = skin != null ? skin.sharedMesh : filter.sharedMesh;
                if (mesh == null) continue;
                count++;
                var slots = renderer.sharedMaterials;
                bool hasSilhouette = slots.Length > 1 && slots[slots.Length - 1] == silhouette;
                if ((!hasSilhouette && slots.Length != mesh.subMeshCount) || Array.Exists(slots, material => material == null))
                    throw new InvalidOperationException($"{GameInfo.LogPrefix} {renderer.name} necesita un material por submalla.");
                int bodySlots = slots.Length - (hasSilhouette ? 1 : 0);
                for (int i = 0; i < bodySlots; i++)
                {
                    var material = slots[i];
                    bool unlit = material.shader.name == "Universal Render Pipeline/Unlit"
                        || material.IsKeywordEnabled("_TIFFANY_UNLIT");
                    if (material.shader.name != ModelLibrary.SpiritShader)
                    {
                        material.shader = shader;
                        material.EnableKeyword("_TIFFANY_CHARACTER");
                    }
                    else material.DisableKeyword("_TIFFANY_CHARACTER");
                    if (unlit) material.EnableKeyword("_TIFFANY_UNLIT");
                    material.SetFloat("_TiffanyStencil", 1f);
                    material.renderQueue = (int)RenderQueue.Geometry + 1;
                    EditorUtility.SetDirty(material);
                }
                if (hasSilhouette) continue;
                if (mesh.subMeshCount > 1)
                {
                    mesh = WithSilhouetteSubmesh(mesh);
                    if (skin != null) skin.sharedMesh = mesh;
                    else filter.sharedMesh = mesh;
                }
                Array.Resize(ref slots, slots.Length + 1);
                slots[slots.Length - 1] = silhouette;
                renderer.sharedMaterials = slots;
            }
            return count;
        }

        static Mesh WithSilhouetteSubmesh(Mesh source)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} La malla {source.name} debe ser un asset.");
            string folder = GeneratedMaterials.folder + "/Silhouettes";
            GeneratedMaterials.EnsureFolder(folder);
            string path = $"{folder}/{guid}_{id}.asset";
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + "Silhouette";
            // Unity repite solo la última submalla cuando sobran materiales.
            mesh.subMeshCount = source.subMeshCount + 1;
            mesh.SetTriangles(source.triangles, source.subMeshCount, false);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                mesh.name = source.name + "Silhouette";
                EditorUtility.SetDirty(mesh);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }
    }
}
