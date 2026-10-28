using System.IO;
using UnityEditor;

namespace TiffanyGame.EditorTools
{
    public class ModelImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if ((!assetPath.StartsWith(ModelLibrary.KenneyFolder + "/", System.StringComparison.Ordinal) &&
                 Path.GetDirectoryName(assetPath).Replace('\\', '/') != ModelLibrary.ModelFolder) ||
                Path.GetExtension(assetPath).ToLowerInvariant() != ".fbx")
                return;

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.preserveHierarchy = true;
            importer.isReadable = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
