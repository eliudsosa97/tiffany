using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class RouteOverview
    {
        public static void CaptureFromCommandLine()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(arguments, "-overviewOut");
                if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-"))
                    throw new ArgumentException("Falta -overviewOut <archivo.png>.");
                Capture(Path.GetFullPath(arguments[index + 1]));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Capture(string output)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var route = GameObject.Find("Route");
            if (route == null) throw new InvalidOperationException("La escena no tiene una raíz Route.");
            Physics.SyncTransforms();
            var bounds = new Bounds(Vector3.zero, new Vector3(60f, 0f, 60f));
            foreach (var renderer in route.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
            foreach (var collider in route.GetComponentsInChildren<Collider>(true)) bounds.Encapsulate(collider.bounds);
            bounds.Expand(new Vector3(20f, 0f, 20f));
            const int width = 2048;
            int height = Mathf.CeilToInt(width * bounds.size.z / bounds.size.x);
            var camera = new GameObject("Route Overview").AddComponent<Camera>();
            var light = new GameObject("Overview Light").AddComponent<Light>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.fog = false;
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1.5f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                camera.enabled = false;
                camera.orthographic = true;
                camera.aspect = (float)width / height;
                camera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / camera.aspect);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = bounds.size.y + 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.12f);
                camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + 100f, bounds.center.z),
                    Quaternion.Euler(90f, 0f, 0f));
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target.Create();
                camera.targetTexture = target;
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("El render pipeline no admite la captura de la vista aérea.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, pixels.EncodeToPNG());
                Debug.Log($"{GameInfo.LogPrefix} Vista aérea guardada en {output} ({width}x{height}).");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(light.gameObject);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
