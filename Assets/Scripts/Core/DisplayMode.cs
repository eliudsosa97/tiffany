using UnityEngine;

namespace TiffanyGame
{
    public static class DisplayRules
    {
        public static bool StartsFullscreen(string[] args, bool isEditor, bool isWebGL)
        {
            if (isEditor || isWebGL) return false;
            if (args != null)
                foreach (string arg in args)
                    if (arg == "-autocapture" || arg == "-capturevideo" || arg == "-playthrough" || arg == "-motiondemo") return false;
            return true;
        }
    }

    public sealed class DisplayMode : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DisplayMode>() != null) return;
            var root = new GameObject("Display Mode", typeof(DisplayMode));
            DontDestroyOnLoad(root);
            if (DisplayRules.StartsFullscreen(System.Environment.GetCommandLineArgs(), Application.isEditor,
                Application.platform == RuntimePlatform.WebGLPlayer))
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }

        void Update()
        {
            if (GameInput.FullscreenTogglePressed) Screen.fullScreen = !Screen.fullScreen;
        }
    }
}
