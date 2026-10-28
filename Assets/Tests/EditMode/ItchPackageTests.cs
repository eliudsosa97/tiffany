using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using TiffanyGame.EditorTools;

namespace TiffanyGame.Tests
{
    public sealed class ItchPackageTests
    {
        string root;
        string buildFolder;
        string zipPath;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), $"tiffany-itch-{Guid.NewGuid():N}");
            buildFolder = Path.Combine(root, "Web");
            zipPath = Path.Combine(root, "tiffany-web.zip");
            Directory.CreateDirectory(Path.Combine(buildFolder, "Build"));
            Directory.CreateDirectory(Path.Combine(buildFolder, "TemplateData"));
            File.WriteAllText(Path.Combine(buildFolder, "index.html"), "web");
            File.WriteAllText(Path.Combine(buildFolder, "Build", "a.data.unityweb"), "data");
            File.WriteAllText(Path.Combine(buildFolder, "TemplateData", "style.css"), "css");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [TestCase(false)]
        [TestCase(true)]
        public void ContentsArePackagedWithoutFolderPrefix(bool streamingAssets)
        {
            if (streamingAssets)
            {
                Directory.CreateDirectory(Path.Combine(buildFolder, "StreamingAssets"));
                File.WriteAllText(Path.Combine(buildFolder, "StreamingAssets", "audio.bin"), "audio");
            }
            ItchPackage.Zip(buildFolder, zipPath);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                Assert.AreEqual(streamingAssets ? 4 : 3, archive.Entries.Count);
                Assert.IsNotNull(archive.GetEntry("index.html"));
                Assert.IsNotNull(archive.GetEntry("Build/a.data.unityweb"));
                Assert.IsNotNull(archive.GetEntry("TemplateData/style.css"));
                if (streamingAssets) Assert.IsNotNull(archive.GetEntry("StreamingAssets/audio.bin"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingIndexThrowsWithoutLeavingZip(bool existingZip)
        {
            File.Delete(Path.Combine(buildFolder, "index.html"));
            File.WriteAllText(Path.Combine(buildFolder, "Build", "index.html"), "nested");
            if (existingZip) File.WriteAllText(zipPath, "previous");

            var exception = Assert.Throws<InvalidOperationException>(() => ItchPackage.Zip(buildFolder, zipPath));

            StringAssert.StartsWith(GameInfo.LogPrefix, exception.Message);
            Assert.IsFalse(File.Exists(zipPath));
        }

        [Test]
        public void PreviousZipIsReplaced()
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                archive.CreateEntry("old.txt");

            ItchPackage.Zip(buildFolder, zipPath);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                Assert.AreEqual(3, archive.Entries.Count);
                Assert.IsNull(archive.GetEntry("old.txt"));
                Assert.IsNotNull(archive.GetEntry("index.html"));
            }
        }
    }
}
