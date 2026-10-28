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
            // Se escribe aparte para que un fallo no deje un zip a medias en la ruta de entrega.
            string partial = zipPath + ".partial";
            File.Delete(partial);
            try
            {
                using (var archive = ZipFile.Open(partial, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.EnumerateFiles(buildFolder, "*", SearchOption.AllDirectories))
                    {
                        string entry = Path.GetRelativePath(buildFolder, file).Replace('\\', '/');
                        archive.CreateEntryFromFile(file, entry, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }
                File.Move(partial, zipPath);
            }
            catch
            {
                if (File.Exists(partial)) File.Delete(partial);
                throw;
            }
            double mb = new FileInfo(zipPath).Length / (1024d * 1024d);
            Debug.Log($"{GameInfo.LogPrefix} Paquete de itch.io: {zipPath}, {mb:F2} MB.");
        }
    }
}
