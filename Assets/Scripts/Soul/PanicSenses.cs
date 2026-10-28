namespace TiffanyGame
{
    public static class PanicSenses
    {
        public static PanicInputs Build(bool lightOn, bool inSafeZone, float witchDistance,
            bool witchVoiceLure, bool hidden, bool playerStill, bool dogNear) => new PanicInputs
        {
            InDarkness = !lightOn && !inSafeZone,
            WitchDistance = witchDistance,
            HearingWhispers = witchVoiceLure,
            InSafeLight = inSafeZone,
            HiddenAndStill = hidden && playerStill,
            DogNear = dogNear
        };
    }
}
