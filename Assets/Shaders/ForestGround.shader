Shader "Universal Render Pipeline/Tiffany Forest Ground"
{
    Properties
    {
        [MainTexture] _BaseMap ("Ground", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Ground normal", 2D) = "bump" {}
        _DetailMap ("Scattered ground", 2D) = "white" {}
        [Normal] _DetailNormal ("Scattered normal", 2D) = "bump" {}
        _Mix ("Scattered proportion", Range(0, 1)) = 0.25
        _NormalStrength ("Normal strength", Range(0, 3)) = 1
        _Cavity ("Cavity contrast", Range(0, 0.75)) = 0
        _Patch ("Feathered patch", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "GroundForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Mix;
                half _NormalStrength;
                half _Cavity;
                half _Patch;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                float3 h = frac(p.xyx * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return 0.55 * Noise(p) + 0.30 * Noise(p * 2 + 11) + 0.15 * Noise(p * 4 + 29);
            }

            half3 Diffuse(Light light, half3 normal)
            {
                return light.color * light.distanceAttenuation * saturate(dot(normal, light.direction));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p = input.positionWS.xz;
                float macro = Fbm(p * 0.16);
                float2 uv = p * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float2 rotated = float2(-uv.y, uv.x) * 1.37 + float2(7.31, 19.17);
                half variation = smoothstep(0.32, 0.68, macro);
                half3 baseColor = lerp(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb,
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, rotated).rgb, variation);
                half3 normalA = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv));
                half3 normalB = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, rotated));
                normalB.xy = half2(normalB.y, -normalB.x);
                half3 normal = lerp(normalA, normalB, variation);
                half blend = _Mix + (Fbm(p * 0.28 + 43) - 0.5) * 2 * min(_Mix, 1 - _Mix);
                half3 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uv * 0.83 + 31).rgb;
                half3 detailNormal = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uv * 0.83 + 31));
                normal = lerp(normal, detailNormal, blend);
                normal.xy *= _NormalStrength;
                half3 normalWS = normalize(normalize(input.normalWS) * max(normal.z, 0.2h)
                    + half3(normal.x, 0, normal.y) * saturate(input.normalWS.y));
                half3 groundColor = lerp(baseColor, detail, blend);
                half luminance = dot(groundColor, half3(0.2126h, 0.7152h, 0.0722h));
                half cavity = 1 - smoothstep(0.04h, 0.32h, luminance);
                half cavityContrast = 1 + _Cavity * (1 - cavity);
                half macroContrast = lerp(0.48h, 1.60h, variation);
                half3 albedo = groundColor * _BaseColor.rgb * macroContrast * cavityContrast;
                half3 lighting = SampleSH(normalWS) + Diffuse(GetMainLight(), normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    lighting += Diffuse(GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1)), normalWS);
                }
                #endif
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    lighting += Diffuse(GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1)), normalWS);
                LIGHT_LOOP_END
                #endif
                half edge = length(input.uv * 2 - 1) + (Noise(p * 2.7) - 0.5) * 0.22;
                half alpha = lerp(1, 1 - smoothstep(0.55h, 0.96h, edge), _Patch);
                half fogFactor = InitializeInputDataFog(float4(input.positionWS, 1), 0);
                return half4(MixFog(albedo * lighting, fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
