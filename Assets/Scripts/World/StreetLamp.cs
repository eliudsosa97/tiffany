using UnityEngine;

namespace TiffanyGame
{
    public sealed class StreetLamp : MonoBehaviour
    {
        public Light lamp;
        public Renderer bulb;
        public int seed;
        public float baseIntensity = 0.8f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        LampFlicker flicker;
        MaterialPropertyBlock properties;
        Color bulbColor;

        void Start()
        {
            flicker = new LampFlicker(seed);
            properties = new MaterialPropertyBlock();
            if (bulb == null) return;
            bulbColor = bulb.sharedMaterial.GetColor(BaseColorId);
            bulb.GetPropertyBlock(properties);
        }

        void Update() => Apply(Time.deltaTime);

        public void Apply(float dt)
        {
            float value = flicker.Tick(dt);
            if (lamp != null) lamp.intensity = baseIntensity * value;
            if (bulb == null) return;
            Color color = bulbColor * value;
            color.a = bulbColor.a;
            properties.SetColor(BaseColorId, color);
            bulb.SetPropertyBlock(properties);
        }
    }
}
