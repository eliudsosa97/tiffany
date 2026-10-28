using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class ItchPackage
    {
        public static void Zip(string buildFolder, string zipPath)
        {
            File.Delete(zipPath);
            if (!File.Exists(Path.Combine(buildFolder, "index.html")))
                throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta index.html en la raíz de {buildFolder}.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath)));
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.EnumerateFiles(buildFolder, "*", SearchOption.AllDirectories))
                {
                    string entry = Path.GetRelativePath(buildFolder, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entry, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
            double mb = new FileInfo(zipPath).Length / (1024d * 1024d);
            Debug.Log($"{GameInfo.LogPrefix} Paquete de itch.io: {zipPath}, {mb:F2} MB.");
        }
    }
}
