using System;

namespace ElVelador
{
    public static class WitchProfiles
    {
        public static WitchProfile For(Chapter chapter)
        {
            var profile = new WitchProfile();
            switch (chapter)
            {
                case Chapter.Prologue:
                case Chapter.Grave:
                case Chapter.Hut:
                    profile.CanLand = false;
                    profile.CanSprint = false;
                    profile.UsesVoiceLure = false;
                    break;
                case Chapter.Trail:
                    profile.CanSprint = false;
                    break;
                case Chapter.Ravine:
                case Chapter.OldCemetery:
                case Chapter.Chapel:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(chapter), chapter, null);
            }
            return profile;
        }
    }
}
