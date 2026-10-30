Shader "Universal Render Pipeline/Tiffany Sight Ring"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.5, 0.08, 0.06, 0.18)
        _Thickness ("Relative thickness", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Thickness;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            float4 Vert(Attributes input) : SV_POSITION
            {
                input.positionOS.xz = normalize(input.positionOS.xz) * (1.0 - input.uv.y * _Thickness);
                return TransformObjectToHClip(input.positionOS);
            }
            half4 Frag() : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
