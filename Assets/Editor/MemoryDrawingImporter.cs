using UnityEditor;

namespace TiffanyGame.EditorTools
{
    public sealed class MemoryDrawingImporter : AssetPostprocessor
    {
        public const int MaxDrawingSize = 1024;

        const string MemoryDrawingsPathPrefix = "Assets/Art/Memories/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(MemoryDrawingsPathPrefix, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = MaxDrawingSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in movedAssets)
            {
                if (!path.StartsWith(MemoryDrawingsPathPrefix, System.StringComparison.Ordinal)) continue;
                if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                    (importer.npotScale != TextureImporterNPOTScale.None || importer.mipmapEnabled))
                    AssetDatabase.ImportAsset(path);
            }
        }
    }
}
