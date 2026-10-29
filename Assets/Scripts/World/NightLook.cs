using UnityEngine;

namespace TiffanyGame
{
    public sealed class NightLook
    {
        public static readonly NightLook Default = new NightLook(0.10f, 0.3f);

        public float MoonIntensity { get; }
        public float AmbientScale { get; }
        public Color AmbientColor { get; }
        public Color FogColor { get; }
        public Color GroundFogColor { get; }
        public float GroundFogLightStrength => 0.35f;

        NightLook(float moonIntensity, float scale)
        {
            MoonIntensity = moonIntensity;
            AmbientScale = scale;
            AmbientColor = new Color(0.025f * scale, 0.035f * scale, 0.065f * scale);
            FogColor = new Color(0.018f * scale, 0.028f * scale, 0.052f * scale);
            GroundFogColor = new Color(0.035f * scale, 0.045f * scale, 0.065f * scale, 0.5f).gamma;
        }
    }
}
