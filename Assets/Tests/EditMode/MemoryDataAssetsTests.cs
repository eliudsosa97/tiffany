using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class MemoryDataAssetsTests
    {
        private const string MemoryDataFolder = "Assets/Data/Memories";
        private const string DrawingsFolder = "Assets/Art/Memories";

        private MemoryCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var assets = new List<TextAsset>();
            foreach (string path in Directory.GetFiles(MemoryDataFolder, "*.json"))
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Assert.IsNotNull(asset, path);
                assets.Add(asset);
            }
            catalog = new MemoryCatalog(assets);
        }

        [Test]
        public void LoadsFiveMemoriesWithDrawings()
        {
            Assert.AreEqual(5, catalog.Count);
            for (int id = 1; id <= 5; id++)
            {
                Assert.IsTrue(catalog.TryGet(id, out MemoryData memory));
                Assert.AreEqual(id, memory.id);
                Assert.IsNotEmpty(memory.drawings);
                foreach (MemoryDrawing drawing in memory.drawings)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(drawing.phrase), drawing.image);
                }
            }
        }

        [Test]
        public void EveryDrawingHasATextureAsset()
        {
            foreach (MemoryData memory in catalog.All)
            {
                foreach (MemoryDrawing drawing in memory.drawings)
                {
                    string path = $"{DrawingsFolder}/{drawing.image}.png";
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path);
                }
            }
        }

        [Test]
        public void DrawingImportersRespectSizeCompressionAndMipmaps()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DrawingsFolder });
            Assert.IsNotEmpty(guids);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.LessOrEqual(importer.maxTextureSize, MemoryDrawingImporter.MaxDrawingSize, path);
                Assert.AreNotEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
            }
        }
    }
}
