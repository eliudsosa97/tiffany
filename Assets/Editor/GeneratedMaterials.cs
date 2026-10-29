using System;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class GeneratedMaterials
    {
        public const string DefaultFolder = "Assets/Scenes/Generated/Materials";
        public static string folder = DefaultFolder;

        public static Material Get(string name, Color color, bool unlit = false, float smoothness = 0.08f, bool? instancing = null)
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
            if (instancing.HasValue) material.enableInstancing = instancing.Value;

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", material.GetColor("_BaseColor"));
            if (!unlit)
            {
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
            }
            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        public static Material GetTextured(string name, Color tint, string textureName, float tiling, float smoothness = 0.08f,
            bool? instancing = null)
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

            var material = Get(name, tint, smoothness: smoothness, instancing: instancing);
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

        public static Material GetGroundFog()
        {
            EnsureFolder(folder);
            var shader = Shader.Find("Universal Render Pipeline/Tiffany Ground Fog");
            if (shader == null)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró el shader de neblina.");
            string path = $"{folder}/GroundFog.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader) { name = "GroundFog" };
            else material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.006f, 0.0075f, 0.014f, 0.7f).gamma);
            material.SetFloat("_Density", FogDensity.BaseDensity);
            material.SetFloat("_NoiseScale", 0.45f);
            material.SetFloat("_NoiseStretch", 1.25f);
            material.SetFloat("_WindSpeed", GroundFog.WindSpeed);
            material.SetFloat("_LightStrength", 0.35f);
            material.SetFloat("_HaloRadius", 4.5f);
            material.SetFloat("_EdgeFade", 0.2f);
            material.SetFloat("_HeightFadeStart", 0.35f);
            material.SetFloat("_HeightFadeEnd", 1.1f);
            material.SetFloat("_CameraFadeStart", 1f);
            material.SetFloat("_CameraFadeEnd", 4f);
            material.SetFloat("_PlayerFadeStart", 0.5f);
            material.SetFloat("_PlayerFadeEnd", 2f);
            material.SetFloat("_PathHalfWidth", FogDensity.PathHalfWidth);
            material.SetFloat("_PathSoftness", FogDensity.PathSoftness);
            material.SetFloat("_PathMultiplier", FogDensity.PathMultiplier);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
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
