Shader "Universal Render Pipeline/Tiffany Water"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.004, 0.008, 0.014, 0.99)
        _WaveScale ("Wave scale", Float) = 1.35
        _WaveStrength ("Wave strength", Range(0, 0.4)) = 0.24
        _WaveSpeed ("Wave speed", Float) = 0.22
        _FlowDirection ("Flow direction", Vector) = (0.45, 0.28, 0, 0)
        _SpecularPower ("Specular power", Range(8, 256)) = 192
        _SpecularStrength ("Specular strength", Range(0, 4)) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WaveScale;
                float _WaveStrength;
                float _WaveSpeed;
                float4 _FlowDirection;
                half _SpecularPower;
                half _SpecularStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float4 waveFrame : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                float3 tangent = TransformObjectToWorldDir(float3(1, 0, 0));
                float3 bitangent = TransformObjectToWorldDir(float3(0, 0, 1));
                output.waveFrame = float4(dot(position.positionWS, tangent), dot(position.positionWS, bitangent), tangent.xz);
                return output;
            }

            float WaveHash(float2 position)
            {
                float3 hash = frac(position.xyx * 0.1031);
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float2 WaveSlope(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                float a = WaveHash(cell);
                float b = WaveHash(cell + float2(1, 0));
                float c = WaveHash(cell + float2(0, 1));
                float d = WaveHash(cell + float2(1, 1));
                float2 blend = fraction * fraction * (3 - 2 * fraction);
                return 6 * fraction * (1 - fraction) * float2(lerp(b - a, d - c, blend.y), lerp(c - a, d - b, blend.x));
            }

            half3 WaterLighting(Light light, half3 normal, half3 viewDirection)
            {
                half diffuse = saturate(dot(normal, light.direction));
                half3 halfway = SafeNormalize(light.direction + viewDirection);
                half specular = pow(saturate(dot(normal, halfway)), _SpecularPower) * _SpecularStrength;
                return (_BaseColor.rgb * diffuse + specular) * light.color * light.distanceAttenuation;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 wave = input.waveFrame.xy * _WaveScale;
                float time = _Time.y * _WaveSpeed;
                float2 flow = _FlowDirection.xy * time;
                float2x2 coarseRotation = float2x2(0.86, 0.51, -0.51, 0.86);
                float2x2 fineRotation = float2x2(0.35, -0.94, 0.94, 0.35);
                float2 coarse = mul(coarseRotation, wave - flow);
                float2 fine = mul(fineRotation, wave - flow * 0.79 - float2(-flow.y, flow.x) * 0.18) * 2.17 + 17.43;
                float2 slopes = (mul(WaveSlope(coarse), coarseRotation)
                    + mul(WaveSlope(fine), fineRotation) * 0.45) * _WaveStrength;
                float2 tangent = input.waveFrame.zw;
                slopes = float2(tangent.x * slopes.x - tangent.y * slopes.y, tangent.y * slopes.x + tangent.x * slopes.y);
                half3 normal = normalize(float3(-slopes.x, 1, -slopes.y));
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = 1 - saturate(dot(normal, viewDirection));
                fresnel *= fresnel;
                half3 color = _BaseColor.rgb * 0.2h + WaterLighting(GetMainLight(), normal, viewDirection);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    color += WaterLighting(GetAdditionalLight(lightIndex, input.positionWS), normal, viewDirection);
                }
                #endif
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    color += WaterLighting(GetAdditionalLight(lightIndex, input.positionWS), normal, viewDirection);
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(color, input.fogFactor), saturate(_BaseColor.a + fresnel * 0.01h));
            }
            ENDHLSL
        }
    }
}
