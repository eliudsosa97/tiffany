using System;

namespace TiffanyGame
{
    public static class WitchProfiles
    {
        public static WitchProfile For(Chapter chapter)
        {
            var profile = new WitchProfile();
            profile.SearchEnabled = profile.LastSeenEnabled = chapter != Chapter.Cemetery;
            profile.CandleWatchEnabled = profile.BarkLureEnabled = chapter == Chapter.TrainTracks
                || chapter == Chapter.Stable || chapter == Chapter.WitchHut;
            profile.AmbushEnabled = chapter == Chapter.Stable || chapter == Chapter.WitchHut;
            switch (chapter)
            {
                case Chapter.Cemetery:
                    profile.CanLand = false;
                    profile.CanSprint = false;
                    profile.UsesVoiceLure = false;
                    profile.FlightHeightOffset = 10f;
                    break;
                case Chapter.WaterPlant:
                    profile.CanSprint = false;
                    profile.UsesVoiceLure = false;
                    break;
                case Chapter.TrainTracks:
                    profile.CanSprint = false;
                    break;
                case Chapter.Stable:
                case Chapter.WitchHut:
                    break;
                case Chapter.Return:
                    profile.GroundChase = true;
                    profile.UsesVoiceLure = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(chapter), chapter, null);
            }
            return profile;
        }
    }
}
