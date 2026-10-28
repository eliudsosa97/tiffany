using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class BuildTools
    {
        public const string WebTemplate = "PROJECT:Tiffany";

        [MenuItem("Tiffany/Build Web")]
        public static void BuildWeb()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = WebTemplate;
            Run(BuildTarget.WebGL, "Builds/Web");
        }

        [MenuItem("Tiffany/Build Mac")]
        public static void BuildMac() => Run(BuildTarget.StandaloneOSX, "Builds/Mac/tiffany.app");

        public static System.Action<BuildTargetGroup, BuildTarget> switchTarget =
            (group, target) => EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

        // Compilar WebGL con otra plataforma activa deja la web sin las variantes de luz de URP.
        public static void UseTarget(BuildTarget active, BuildTarget target)
        {
            if (active != target) switchTarget(BuildPipeline.GetBuildTargetGroup(target), target);
        }

        static void Run(BuildTarget target, string path)
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError($"{GameInfo.LogPrefix} No hay escenas habilitadas para la compilación.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            UseTarget(EditorUserBuildSettings.activeBuildTarget, target);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"{GameInfo.LogPrefix} Compilación de {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):F2} MB, {summary.totalErrors} errores.");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
