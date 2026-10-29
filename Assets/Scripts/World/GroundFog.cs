using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class GroundFog : MonoBehaviour
    {
        public const string RootName = "Ground Fog";
        public const float WindSpeed = 0.32f;
        public const float WaterWindSpeed = 0.2f;

        [Serializable]
        public struct Layer
        {
            public Renderer renderer;
            public float groundHeight;
            public float density;
            public bool water;
            public Vector4 waterFootprint;
            public Vector2 waterAxis;
            public Vector3 pathStart;
            public Vector3 pathCorner;
            public Vector3 pathEnd;
        }

        public Transform player;
        public Layer[] layers = Array.Empty<Layer>();

        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int GroundHeightId = Shader.PropertyToID("_GroundHeight");
        static readonly int WindId = Shader.PropertyToID("_WindSpeed");
        static readonly int PathMultiplierId = Shader.PropertyToID("_PathMultiplier");
        static readonly int WaterLayerId = Shader.PropertyToID("_WaterLayer");
        static readonly int WaterFootprintId = Shader.PropertyToID("_WaterFootprint");
        static readonly int WaterAxisId = Shader.PropertyToID("_WaterAxis");
        static readonly int StartId = Shader.PropertyToID("_PathStart");
        static readonly int CornerId = Shader.PropertyToID("_PathCorner");
        static readonly int EndId = Shader.PropertyToID("_PathEnd");
        static readonly int PlayerId = Shader.PropertyToID("_GroundFogPlayer");
        MaterialPropertyBlock properties;

        public void ApplyProperties()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.renderer == null) continue;
                properties.Clear();
                properties.SetFloat(DensityId, layer.density);
                properties.SetFloat(GroundHeightId, layer.groundHeight);
                properties.SetFloat(WindId, layer.water ? WaterWindSpeed : WindSpeed);
                properties.SetFloat(PathMultiplierId, layer.water ? 1f : FogDensity.PathMultiplier);
                properties.SetFloat(WaterLayerId, layer.water ? 1f : 0f);
                properties.SetVector(WaterFootprintId, layer.waterFootprint);
                properties.SetVector(WaterAxisId, new Vector4(layer.waterAxis.x, layer.waterAxis.y, 0f, 0f));
                properties.SetVector(StartId, layer.pathStart);
                properties.SetVector(CornerId, layer.pathCorner);
                properties.SetVector(EndId, layer.pathEnd);
                layer.renderer.SetPropertyBlock(properties);
            }
        }

        void OnEnable() => ApplyProperties();

        void LateUpdate()
        {
            Vector3 position = player != null ? player.position : Vector3.zero;
            Shader.SetGlobalVector(PlayerId, new Vector4(position.x, position.y, position.z, player != null ? 1f : 0f));
        }

        void OnDisable() => Shader.SetGlobalVector(PlayerId, Vector4.zero);
    }
}
