using System;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class GeneratedMaterials
    {
        public const string DefaultFolder = "Assets/Scenes/Generated/Materials";
        public static string folder = DefaultFolder;

        public static Material Get(string name, Color color, bool unlit = false, float smoothness = 0.08f)
        {
            EnsureFolder(folder);
            string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el shader '{shaderName}'.");

            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader) { name = name };
            else material.shader = shader;

            material.SetColor("_BaseColor", color);
            if (!unlit)
            {
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
            }
            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        public static Material GetTextured(string name, Color tint, string textureName, float tiling, float smoothness = 0.08f)
        {
            string textureFolder = $"Assets/Art/CC0/PolyHaven/{textureName}";
            string diffusePath = $"{textureFolder}/{textureName}_diff.jpg";
            string normalPath = $"{textureFolder}/{textureName}_nor_gl.jpg";
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró la textura difusa en '{diffusePath}'.");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró la textura normal en '{normalPath}'.");

            var material = Get(name, tint, smoothness: smoothness);
            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 1f);
            var scale = new Vector2(tiling, tiling);
            material.mainTextureScale = scale;
            material.SetTextureScale("_BumpMap", scale);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material GetWater(bool river = false)
        {
            EnsureFolder(folder);
            var shader = Shader.Find("Universal Render Pipeline/Tiffany Water");
            if (shader == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el shader del agua.");
            string name = river ? "RiverWater" : "RouteWater";
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader) { name = name };
            else material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.004f, 0.008f, 0.014f, 0.99f));
            material.SetFloat("_WaveScale", 1.35f);
            material.SetFloat("_WaveStrength", 0.24f);
            material.SetFloat("_WaveSpeed", river ? 0.7f : 0.22f);
            material.SetVector("_FlowDirection", river ? new Vector4(1f, 0f, 0f, 0f) : new Vector4(0.45f, 0.28f, 0f, 0f));
            material.SetFloat("_SpecularPower", 192f);
            material.SetFloat("_SpecularStrength", 0.4f);
            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
