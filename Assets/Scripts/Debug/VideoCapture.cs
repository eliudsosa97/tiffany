using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-1000)]
    public class VideoCapture : MonoBehaviour
    {
        const int FrameRate = 30;
        string directory = string.Empty;
        float seconds = 10f;
        int previousCaptureFramerate;
        bool captureFramerateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = System.Environment.GetCommandLineArgs();
            string directory = null;
            float seconds = 10f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-capturevideo") directory = args[i + 1];
                else if (args[i] == "-captureseconds" &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                    value > 0f && !float.IsInfinity(value)) seconds = value;
            }

            if (directory == null) return;
            directory = Path.GetFullPath(directory);
            var go = new GameObject("VideoCapture");
            DontDestroyOnLoad(go);
            var capture = go.AddComponent<VideoCapture>();
            capture.directory = directory;
            capture.seconds = seconds;
#endif
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(directory)) yield break;
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null) gameInterface.StartGame();
            yield return null;
            Directory.CreateDirectory(directory);
            previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = FrameRate;
            captureFramerateChanged = true;

            int totalFrames = Mathf.CeilToInt(seconds * FrameRate);
            var endOfFrame = new WaitForEndOfFrame();
            for (int frame = 1; frame <= totalFrames; frame++)
            {
                yield return endOfFrame;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, $"frame_{frame:00000}.png"));
            }

            RestoreCaptureFramerate();
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"{GameInfo.LogPrefix} Video terminado: {totalFrames} cuadros en {directory}.");
            Application.Quit();
        }

        void OnDisable() => RestoreCaptureFramerate();

        void RestoreCaptureFramerate()
        {
            if (!captureFramerateChanged) return;
            Time.captureFramerate = previousCaptureFramerate;
            captureFramerateChanged = false;
        }
    }
}
