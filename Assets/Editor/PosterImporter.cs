using System;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public sealed class PosterImporter : AssetPostprocessor
    {
        static bool IsPoster(string path) => path.StartsWith(PosterBuilder.Folder + "/", StringComparison.Ordinal)
            && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!IsPoster(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in movedAssets)
                if (IsPoster(path) && AssetImporter.GetAtPath(path) is TextureImporter importer
                    && (importer.maxTextureSize != 1024 || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                        || importer.textureCompression != TextureImporterCompression.Compressed || importer.npotScale != TextureImporterNPOTScale.None
                        || importer.textureType != TextureImporterType.Default)) AssetDatabase.ImportAsset(path);
        }
    }
}
