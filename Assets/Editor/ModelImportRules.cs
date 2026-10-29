using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace TiffanyGame.EditorTools
{
    public class ModelImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 5;

        bool IsLibraryModel => Path.GetDirectoryName(assetPath).Replace('\\', '/') == ModelLibrary.ModelFolder &&
                               Path.GetExtension(assetPath).ToLowerInvariant() == ".fbx";

        bool IsKenneyNature => assetPath.StartsWith(ModelLibrary.KenneyFolder + "/Nature/", System.StringComparison.Ordinal) &&
                               Path.GetExtension(assetPath).ToLowerInvariant() == ".fbx";

        ModelLibrary.ModelSpec Spec()
        {
            string id = Path.GetFileNameWithoutExtension(assetPath);
            return File.Exists(ModelLibrary.SpecPath(id)) ? ModelLibrary.LoadSpec(id) : null;
        }

        void OnPreprocessModel()
        {
            if (!IsLibraryModel && !IsKenneyNature) return;

            var spec = Spec();
            var importer = (ModelImporter)assetImporter;
            bool rigged = spec?.Rigged ?? false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = rigged;
            importer.animationType = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (rigged)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                importer.animationRotationError = 0.1f;
                importer.animationPositionError = 0.1f;
                importer.animationScaleError = 0.1f;
                if (!string.IsNullOrEmpty(spec.rootMotionBone)) importer.motionNodeName = spec.rootMotionBone;
            }
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.preserveHierarchy = true;
            importer.isReadable = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = spec?.bakeAxisConversion ?? false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessAnimation()
        {
            if (!IsLibraryModel) return;
            var spec = Spec();
            if (spec == null || !spec.Rigged) return;

            var importer = (ModelImporter)assetImporter;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var clip in importer.defaultClipAnimations)
            {
                // Blender nombra la toma "Armadura|Acción" o solo la acción.
                string name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
                var entry = System.Array.Find(spec.clips, c => c.name == name);
                if (entry == null) continue;
                clip.name = name;
                clip.firstFrame = 0f;
                clip.lastFrame = entry.frames;
                clip.loopTime = entry.loop;
                clip.loopPose = false;
                clip.wrapMode = entry.loop ? UnityEngine.WrapMode.Loop : UnityEngine.WrapMode.ClampForever;
                if (!string.IsNullOrEmpty(spec.rootMotionBone))
                {
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
        }
    }
}
