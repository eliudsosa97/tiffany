using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class NightLook
    {
        public const float FogStartDistance = 40f;
        public const float FogEndDistance = 76f;
        public static readonly NightLook Current = new NightLook("Current", 0.32f, 1f);
        public static readonly NightLook A = new NightLook("A", 0.18f, 0.5f);
        public static readonly NightLook B = new NightLook("B", 0.10f, 0.3f);
        public static readonly NightLook C = new NightLook("C", 0.05f, 0.15f);
        public static NightLook Default => Current;
        public static readonly IReadOnlyList<NightLook> Presets = Array.AsReadOnly(new[] { Current, A, B, C });

        public string Name { get; }
        public float MoonIntensity { get; }
        public Color AmbientColor { get; }
        public Color FogColor { get; }
        public Color GroundFogColor { get; }
        public float GroundFogLightStrength => 0.35f;

        NightLook(string name, float moonIntensity, float scale)
        {
            Name = name;
            MoonIntensity = moonIntensity;
            AmbientColor = new Color(0.025f * scale, 0.035f * scale, 0.065f * scale);
            FogColor = new Color(0.018f * scale, 0.028f * scale, 0.052f * scale);
            GroundFogColor = new Color(0.035f * scale, 0.045f * scale, 0.065f * scale, 0.5f).gamma;
        }
    }
}
