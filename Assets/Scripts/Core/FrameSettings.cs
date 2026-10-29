namespace TiffanyGame
{
    public static class FrameSettings
    {
        public const int DesktopFrameRate = 60;

        public static int TargetFrameRate(bool webGL) => webGL ? -1 : DesktopFrameRate;

        public static bool RunInBackground(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args)
                if (arg == "-autocapture" || arg == "-capturevideo" || arg == "-playthrough") return true;
            return false;
        }
    }
}
