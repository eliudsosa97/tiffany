using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame
{
    public class AutoCapture : MonoBehaviour
    {
        string directory = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-autocapture") continue;
                string directory = Path.GetFullPath(args[i + 1]);
                var go = new GameObject("AutoCapture");
                DontDestroyOnLoad(go);
                go.AddComponent<AutoCapture>().directory = directory;
                return;
            }
#endif
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(directory)) yield break;
            GameInput.MoveOverride = Vector2.zero;
            GameInput.SlowOverride = false;
            GameInput.RunOverride = false;
            GameInput.AimOverride = Vector2.zero;
            Directory.CreateDirectory(directory);
            Debug.Log($"{GameInfo.LogPrefix} Calidad: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline: "
                + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "ninguno"));
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("00_start");

            var followCamera = FindAnyObjectByType<FollowCamera>();
            var stops = FindObjectsByType<CaptureStop>(FindObjectsSortMode.None);
            System.Array.Sort(stops, (a, b) => string.CompareOrdinal(a.name, b.name));
            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<CharacterController>() : null;

            for (int i = 0; i < stops.Length; i++)
            {
                var stop = stops[i];
                if (player != null)
                {
                    bool controllerWasEnabled = controller != null && controller.enabled;
                    if (controller != null) controller.enabled = false;
                    player.transform.SetPositionAndRotation(stop.transform.position, stop.transform.rotation);
                    if (controller != null) controller.enabled = controllerWasEnabled;
                    Physics.SyncTransforms();
                }

                if (stop.overrideCamera)
                {
                    if (followCamera != null) followCamera.enabled = false;
                    var camera = Camera.main;
                    if (camera != null) camera.transform.SetPositionAndRotation(stop.cameraPosition, stop.cameraRotation);
                }
                else if (followCamera != null)
                {
                    followCamera.enabled = true;
                    followCamera.Snap();
                }

                var witchStage = stop.GetComponent<WitchCaptureStage>();
                if (witchStage != null && player != null) witchStage.Apply(player.transform);
                var stormStage = stop.GetComponent<StormCaptureStage>();
                if (stormStage != null) stormStage.Apply();
                var lightStage = stop.GetComponent<LightCaptureStage>();
                if (lightStage != null && player != null) lightStage.Apply(player.transform);
                yield return new WaitForSecondsRealtime(stop.wait);
                yield return Capture($"{i + 1:00}_{stop.name}");
                if (witchStage != null) witchStage.Clear();
                if (stormStage != null) stormStage.Clear();
                if (lightStage != null) lightStage.Clear();
            }

            Debug.Log($"{GameInfo.LogPrefix} Recorrido terminado: {stops.Length + 1} capturas en {directory}.");
            yield return new WaitForSecondsRealtime(1f);
            GameInput.ClearOverrides();
            Application.Quit();
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            Debug.Log($"{GameInfo.LogPrefix} Captura: {name}");
            yield return null;
        }
    }
}
