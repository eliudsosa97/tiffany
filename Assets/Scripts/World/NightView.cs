using System;
using System.Collections.Generic;

namespace TiffanyGame
{
    public sealed class NightView
    {
        public static readonly IReadOnlyList<NightView> Combinations = Array.AsReadOnly(new[]
        {
            new NightView("Hoy", 36f, NightLook.Current, NightLook.FogStartDistance, NightLook.FogEndDistance),
            new NightView("D28B", 28f, NightLook.B),
            new NightView("D24B", 24f, NightLook.B),
            new NightView("D24C", 24f, NightLook.C)
        });

        public string Name { get; }
        public float CameraDistance { get; }
        public NightLook Look { get; }
        public float FogStartDistance { get; }
        public float FogEndDistance { get; }
        public float FarClipPlane => FogEndDistance + 4f;

        NightView(string name, float cameraDistance, NightLook look)
            : this(name, cameraDistance, look, cameraDistance + 4f, cameraDistance + 24f) { }

        NightView(string name, float cameraDistance, NightLook look, float fogStartDistance, float fogEndDistance)
        {
            Name = name;
            CameraDistance = cameraDistance;
            Look = look;
            FogStartDistance = fogStartDistance;
            FogEndDistance = fogEndDistance;
        }
    }
}
