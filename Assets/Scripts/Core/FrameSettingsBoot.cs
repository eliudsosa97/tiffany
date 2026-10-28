using UnityEngine;

namespace TiffanyGame
{
    public static class FrameSettingsBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Application.isEditor) return;
            int fps = FrameSettings.TargetFrameRate(false);
            bool runInBackground = FrameSettings.RunInBackground(System.Environment.GetCommandLineArgs());
            Application.targetFrameRate = fps;
            Application.runInBackground = runInBackground;
            Debug.Log($"{GameInfo.LogPrefix} Fotogramas: {fps}, en segundo plano: {runInBackground}");
#endif
        }
    }
}
