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
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                if (slots.Length == 2 && slots[1] == silhouette) continue;
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                // Unity repite solo la última submalla al añadir un material.
                if (mesh == null || mesh.subMeshCount != 1 || slots.Length != 1 || slots[0] == null)
                    throw new InvalidOperationException($"{GameInfo.LogPrefix} {renderer.name} necesita una sola submalla para la silueta.");
                var material = slots[0];
                bool unlit = material.shader.name == "Universal Render Pipeline/Unlit";
                material.shader = shader;
                material.EnableKeyword("_TIFFANY_CHARACTER");
                if (unlit) material.EnableKeyword("_TIFFANY_UNLIT");
                material.SetFloat("_TiffanyStencil", 1f);
                material.renderQueue = (int)RenderQueue.Geometry + 1;
                EditorUtility.SetDirty(material);
                renderer.sharedMaterials = new[] { material, silhouette };
            }
            return renderers.Length;
        }
    }
}
