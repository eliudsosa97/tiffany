namespace TiffanyGame
{
    public static class HidingRules
    {
        public static bool NextHidden(bool hidden, bool interactPressed, bool insideVolume)
        {
            return interactPressed ? !hidden && insideVolume : hidden;
        }
    }
}
