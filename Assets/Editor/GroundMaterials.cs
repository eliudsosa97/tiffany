using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.EditorTools
{
    public static class GroundMaterials
    {
        public const float TileMeters = 2.85f;
        const string ShaderName = "Universal Render Pipeline/Tiffany Forest Ground";

        public static Material For(GroundType type)
        {
            var (texture, tint, accent, mix) = type switch
            {
                GroundType.Dirt => ("dry_ground_rocks", "B6AB93", "gravel_ground_01", 0.25f),
                GroundType.Grass => ("dry_ground_rocks", "919171", "dry_decay_leaves", 0.30f),
                GroundType.Slab => ("rock_boulder_dry", "A8A69B", "dry_ground_rocks", 0.15f),
                GroundType.Gravel => ("gravel_ground_01", "C1BAAA", "dry_ground_rocks", 0.25f),
                GroundType.Leaves => ("dry_decay_leaves", "A58B6B", "dry_ground_rocks", 0.25f),
                GroundType.Branches => ("burned_ground_01", "8B7965", "dry_decay_leaves", 0.35f),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            return Surface("Route" + type, texture, tint, accent, mix);
        }

        internal static Material TreePatch(string name, string texture, string tint, string accent, float mix) =>
            Surface(name, texture, tint, accent, mix, true);

        static Material Surface(string name, string texture, string tint, string accent, float mix, bool patch = false)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el shader del suelo del bosque.");
            ColorUtility.TryParseHtmlString("#" + tint, out var color);
            string detailFolder = $"Assets/Art/CC0/PolyHaven/{accent}/{accent}";
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(detailFolder + "_diff.jpg");
            var detailNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(detailFolder + "_nor_gl.jpg");
            if (detail == null || detailNormal == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} Faltan las texturas del suelo {accent}.");
            var material = GeneratedMaterials.GetTextured(name, color, texture, 1f / TileMeters, instancing: true);
            material.shader = shader;
            material.DisableKeyword("_NORMALMAP");
            material.SetTexture("_DetailMap", detail);
            material.SetTexture("_DetailNormal", detailNormal);
            material.SetFloat("_Mix", mix);
            material.SetFloat("_Patch", patch ? 1f : 0f);
            material.SetFloat("_SrcBlend", (float)(patch ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(patch ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", patch ? 0f : 1f);
            material.renderQueue = (int)RenderQueue.Geometry + (patch ? 10 : 0);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
