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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
