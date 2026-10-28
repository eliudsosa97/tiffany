using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class BuildTools
    {
        public const string WebTemplate = "PROJECT:Tiffany";

        [MenuItem("Tiffany/Build Web")]
        public static void BuildWeb() => RunWeb();

        [MenuItem("Tiffany/Build itch.io")]
        public static void BuildItch() => RunWeb(() => ItchPackage.Zip("Builds/Web", "Builds/tiffany-web.zip"));

        [MenuItem("Tiffany/Build itch.io (debug local)")]
        public static void BuildItchDebug() => RunWeb(() => ItchPackage.Zip("Builds/WebDebug", "Builds/tiffany-web-debug.zip"), debug: true);

        public static BuildPlayerOptions WebOptions(string[] scenes, bool debug) => new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = debug ? "Builds/WebDebug" : "Builds/Web",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
            extraScriptingDefines = debug ? new[] { WebDebugQuery.Define } : Array.Empty<string>()
        };

        public static bool LeaksWebDebug(string playerDefines, bool debug)
        {
            if (debug || string.IsNullOrEmpty(playerDefines)) return false;
            foreach (string define in playerDefines.Split(';'))
                if (define.Trim() == WebDebugQuery.Define) return true;
            return false;
        }

        static void RunWeb(Action afterBuild = null, bool debug = false)
        {
            if (LeaksWebDebug(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL), debug))
            {
                Debug.LogError($"{GameInfo.LogPrefix} {WebDebugQuery.Define} está en Player Settings: la build pública abriría ?playthrough.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = WebTemplate;
            Run(BuildTarget.WebGL, null, afterBuild, debug);
        }

        [MenuItem("Tiffany/Build Mac")]
        public static void BuildMac() => Run(BuildTarget.StandaloneOSX, "Builds/Mac/tiffany.app");

        // Compilar WebGL con otra plataforma activa deja la web sin las variantes de luz de URP.
        public static bool UseTarget(BuildTarget active, BuildTarget target, Func<BuildTargetGroup, BuildTarget, bool> switchTarget) =>
            active == target || switchTarget(BuildPipeline.GetBuildTargetGroup(target), target);

        static void Run(BuildTarget target, string path, Action afterBuild = null, bool webDebug = false)
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

            if (!UseTarget(EditorUserBuildSettings.activeBuildTarget, target, EditorUserBuildSettings.SwitchActiveBuildTarget))
            {
                Debug.LogError($"{GameInfo.LogPrefix} No se pudo cambiar la plataforma activa a {target}.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            var options = target == BuildTarget.WebGL ? WebOptions(scenes.ToArray(), webDebug) : new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"{GameInfo.LogPrefix} Compilación de {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):F2} MB, {summary.totalErrors} errores.");
            bool succeeded = summary.result == BuildResult.Succeeded;
            if (succeeded && afterBuild != null)
            {
                try { afterBuild(); }
                catch (Exception exception)
                {
                    Debug.LogError($"{GameInfo.LogPrefix} No se pudo crear el paquete de itch.io: {exception}");
                    succeeded = false;
                }
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
