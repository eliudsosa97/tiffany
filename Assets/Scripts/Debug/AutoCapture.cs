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
            var gameInterface = FindAnyObjectByType<GameInterface>();
            yield return Capture("00_title");
            if (gameInterface != null) gameInterface.StartGame();
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("00_start");

            var followCamera = FindAnyObjectByType<FollowCamera>();
            var stops = FindObjectsByType<CaptureStop>(FindObjectsSortMode.None);
            System.Array.Sort(stops, CompareStops);
            var player = GameObject.FindGameObjectWithTag("Player");

            for (int i = 0; i < stops.Length; i++)
            {
                var stop = stops[i];
                var echoStage = stop.GetComponent<EchoCaptureStage>();
                if (echoStage != null)
                {
                    if (!echoStage.Apply())
                        Debug.LogError($"{GameInfo.LogPrefix} No se pudo preparar {stop.name}.");
                    else
                    {
                        yield return new WaitForSeconds(stop.wait);
                        yield return Capture($"{i + 1:00}_{stop.name}");
                        echoStage.Clear();
                    }
                    continue;
                }
                var occlusionStage = stop.GetComponent<OcclusionCaptureStage>();
                if (occlusionStage != null && player != null)
                {
                    try
                    {
                        yield return occlusionStage.Prepare(player.transform, followCamera);
                        yield return new WaitForSecondsRealtime(stop.wait);
                        yield return Capture($"{i + 1:00}_{stop.name}");
                    }
                    finally { occlusionStage.Clear(); }
                    continue;
                }
                stop.ApplyPose(player != null ? player.transform : null, followCamera);

                var awayStage = stop.GetComponent<WitchAwayStage>();
                if (awayStage != null) awayStage.Apply();
                var witchStage = stop.GetComponent<WitchCaptureStage>();
                if (witchStage != null && player != null) witchStage.Apply(player.transform);
                var tylerStage = stop.GetComponent<TylerCaptureStage>();
                if (tylerStage != null && player != null) tylerStage.Apply(player.transform);
                var stormStage = stop.GetComponent<StormCaptureStage>();
                if (stormStage != null) stormStage.Apply();
                var lightStage = stop.GetComponent<LightCaptureStage>();
                if (lightStage != null && player != null) lightStage.Apply(player.transform);
                var fogStage = stop.GetComponent<FogCaptureStage>();
                if (fogStage != null && !fogStage.Apply())
                    Debug.LogError($"{GameInfo.LogPrefix} No se pudo preparar {stop.name}.");
                var hideStage = stop.GetComponent<HideCaptureStage>();
                if (hideStage != null) hideStage.Apply();
                var powerStage = stop.GetComponent<PowerCaptureStage>();
                if (powerStage != null && player != null) powerStage.Apply(player.transform);
                var challengeStage = stop.GetComponent<ChallengeCaptureStage>();
                if (challengeStage != null) challengeStage.Apply();
                var memoryStage = stop.GetComponent<MemoryCaptureStage>();
                if (memoryStage != null) memoryStage.Apply();
                var panicStage = stop.GetComponent<PanicCaptureStage>();
                if (panicStage != null && player != null) panicStage.Apply(player.transform);
                var caughtStage = stop.GetComponent<CaughtCaptureStage>();
                if (caughtStage != null && player != null) caughtStage.Apply(player.transform);
                var endingStage = stop.GetComponent<EndingCaptureStage>();
                if (endingStage != null) endingStage.Apply();
                yield return new WaitForSecondsRealtime(stop.wait);
                yield return Capture($"{i + 1:00}_{stop.name}");
                if (witchStage != null) witchStage.Clear();
                if (tylerStage != null) tylerStage.Clear();
                if (stormStage != null) stormStage.Clear();
                if (lightStage != null) lightStage.Clear();
                if (fogStage != null) fogStage.Clear();
                if (hideStage != null) hideStage.Clear();
                if (powerStage != null) powerStage.Clear();
                if (challengeStage != null) challengeStage.Clear();
                if (memoryStage != null) memoryStage.Clear();
                if (panicStage != null) panicStage.Clear();
                if (caughtStage != null) yield return caughtStage.Clear();
                if (endingStage != null) endingStage.Clear();
                if (awayStage != null) awayStage.Clear();
            }

            int captureCount = stops.Length + 1;
            if (gameInterface != null)
            {
                gameInterface.MemoryFound(2);
                gameInterface.MemoryFound(1);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture($"{captureCount++:00}_notice");
                gameInterface.ShowSubtitle("Subtítulo de prueba", 5f);
                yield return Capture($"{captureCount++:00}_subtitle");
                gameInterface.ToggleNotebook();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture($"{captureCount++:00}_notebook_1");
                gameInterface.NextPage();
                yield return Capture($"{captureCount++:00}_notebook_2");
                gameInterface.ToggleNotebook();
                gameInterface.TogglePause();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture($"{captureCount++:00}_pause");
                gameInterface.TogglePause();
            }

            Debug.Log($"{GameInfo.LogPrefix} Recorrido terminado: {captureCount + 1} capturas en {directory}.");
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

        public static int CompareStops(CaptureStop a, CaptureStop b)
        {
            var left = a.GetComponent<ChallengeCaptureStage>();
            var right = b.GetComponent<ChallengeCaptureStage>();
            if (left != null && right != null && left.challenge == right.challenge) return left.after.CompareTo(right.after);
            var leftCover = a.GetComponent<OcclusionCaptureStage>();
            var rightCover = b.GetComponent<OcclusionCaptureStage>();
            if (leftCover != null && rightCover != null && a.transform.parent == b.transform.parent)
                return leftCover.after.CompareTo(rightCover.after);
            return string.CompareOrdinal(a.name, b.name);
        }
    }
}
