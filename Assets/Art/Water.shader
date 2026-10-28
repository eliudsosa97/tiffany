Shader "Universal Render Pipeline/Tiffany Water"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.018, 0.038, 0.05, 0.88)
        _WaveScale ("Wave scale", Float) = 3
        _WaveStrength ("Wave strength", Range(0, 0.4)) = 0.14
        _WaveSpeed ("Wave speed", Float) = 0.6
        _SpecularPower ("Specular power", Range(8, 128)) = 48
        _SpecularStrength ("Specular strength", Range(0, 4)) = 2
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
                half _SpecularPower;
                half _SpecularStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
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
                float2 wave = input.positionWS.xz * _WaveScale;
                float time = _Time.y * _WaveSpeed;
                float2 slopes = float2(cos(wave.x + wave.y * 0.7 + time),
                    cos(wave.y * 1.3 - wave.x * 0.5 - time * 0.8)) * _WaveStrength;
                half3 normal = normalize(float3(slopes.x, 1, slopes.y));
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = 1 - saturate(dot(normal, viewDirection));
                fresnel *= fresnel;
                half3 color = _BaseColor.rgb * (0.35h + fresnel) + WaterLighting(GetMainLight(), normal, viewDirection);

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
                return half4(MixFog(color, input.fogFactor), saturate(_BaseColor.a + fresnel * 0.1h));
            }
            ENDHLSL
        }
    }
}
