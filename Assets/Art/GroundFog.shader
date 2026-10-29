Shader "Universal Render Pipeline/Tiffany Ground Fog"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.3811, 0.3959, 0.4100, 0.45)
        _Density ("Density", Range(0, 1)) = 0.78
        _NoiseScale ("Noise scale", Float) = 0.65
        _NoiseStretch ("Noise stretch", Float) = 2.5
        _WindSpeed ("Wind speed", Float) = 0.32
        _LightStrength ("Light strength", Float) = 0.35
        _HaloRadius ("Halo radius", Float) = 4.5
        _MoonStrength ("Moon strength", Float) = 0.08
        _EdgeFade ("Edge fade", Range(0.01, 0.5)) = 0.2
        _HeightFadeStart ("Height fade start", Float) = 0.35
        _HeightFadeEnd ("Height fade end", Float) = 1.1
        _CameraFadeStart ("Camera fade start", Float) = 1
        _CameraFadeEnd ("Camera fade end", Float) = 4
        _PathHalfWidth ("Path half width", Float) = 2.5
        _PathSoftness ("Path softness", Float) = 3
        _PathMultiplier ("Path multiplier", Range(0, 1)) = 0.85
        _PlayerFadeStart ("Player fade start", Float) = 0.5
        _PlayerFadeEnd ("Player fade end", Float) = 2
        [HideInInspector] _GroundHeight ("Ground height", Float) = 0
        [HideInInspector] _WaterLayer ("Water layer", Float) = 0
        [HideInInspector] _WaterFootprint ("Water footprint", Vector) = (0, 0, 0, 0)
        [HideInInspector] _WaterAxis ("Water axis", Vector) = (1, 0, 0, 0)
        [HideInInspector] _PathStart ("Path start", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PathCorner ("Path corner", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PathEnd ("Path end", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Pass
        {
            Name "GroundFogForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                float _Density;
                float _NoiseScale;
                float _NoiseStretch;
                float _WindSpeed;
                half _LightStrength;
                float _HaloRadius;
                half _MoonStrength;
                float _EdgeFade;
                float _HeightFadeStart;
                float _HeightFadeEnd;
                float _CameraFadeStart;
                float _CameraFadeEnd;
                float _PathHalfWidth;
                float _PathSoftness;
                float _PathMultiplier;
                float _PlayerFadeStart;
                float _PlayerFadeEnd;
                float _GroundHeight;
                float _WaterLayer;
                float4 _WaterFootprint;
                float4 _WaterAxis;
                float4 _PathStart;
                float4 _PathCorner;
                float4 _PathEnd;
            CBUFFER_END
            float4 _GroundFogPlayer;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float FogHash(float2 position)
            {
                float3 hash = frac(position.xyx * 0.1031);
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float FogNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 fraction = frac(position);
                float2 blend = fraction * fraction * (3 - 2 * fraction);
                return lerp(lerp(FogHash(cell), FogHash(cell + float2(1, 0)), blend.x),
                    lerp(FogHash(cell + float2(0, 1)), FogHash(cell + float2(1, 1)), blend.x), blend.y);
            }

            float PathDistance(float2 samplePosition, float2 segmentStart, float2 segmentEnd)
            {
                float2 segment = segmentEnd - segmentStart;
                float along = saturate(dot(samplePosition - segmentStart, segment) / max(dot(segment, segment), 0.0001));
                return length(samplePosition - segmentStart - segment * along);
            }

            half3 FogLighting(uint lightIndex, float3 positionWS)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS);
                #if USE_CLUSTER_LIGHT_LOOP
                int index = lightIndex;
                #else
                int index = GetPerObjectLightIndex(lightIndex);
                #endif
                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 source = _AdditionalLightsBuffer[index].position;
                #else
                float4 source = _AdditionalLightsPosition[index];
                #endif
                if (source.w == 0) return light.color * _MoonStrength;
                float3 offset = source.xyz - positionWS;
                float halo = 1 - smoothstep(0, _HaloRadius, length(offset.xz));
                float3 glow = light.color * light.distanceAttenuation * _LightStrength;
                return glow / (1 + max(glow.r, max(glow.g, glow.b))) * (halo * 0.3);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 edge = min(input.uv, 1 - input.uv);
                float fade = smoothstep(0, _EdgeFade, min(edge.x, edge.y));
                fade *= smoothstep(_CameraFadeStart, _CameraFadeEnd, distance(input.positionWS, GetCameraPositionWS()));
                fade *= 1 - smoothstep(_HeightFadeStart, _HeightFadeEnd, input.positionWS.y - _GroundHeight);
                clip(fade - 0.002);
                float2 samplePosition = input.positionWS.xz;
                if (_WaterFootprint.z > 0)
                {
                    float2 offset = samplePosition - _WaterFootprint.xy;
                    float2 waterPosition = float2(dot(offset, _WaterAxis.xy), dot(offset, float2(-_WaterAxis.y, _WaterAxis.x)));
                    float2 shore = abs(waterPosition) - _WaterFootprint.zw;
                    float land = smoothstep(-1, 1, max(shore.x, shore.y));
                    fade *= lerp(land, 1 - land, _WaterLayer);
                }
                float path = min(PathDistance(samplePosition, _PathStart.xz, _PathCorner.xz), PathDistance(samplePosition, _PathCorner.xz, _PathEnd.xz));
                float nearPath = 1 - smoothstep(_PathHalfWidth, _PathHalfWidth + _PathSoftness, path);
                float playerDistance = distance(samplePosition, _GroundFogPlayer.xz);
                float playerFade = lerp(0.3, 1, smoothstep(_PlayerFadeStart, _PlayerFadeEnd, playerDistance));
                fade *= lerp(1, playerFade, _GroundFogPlayer.w);
                fade *= lerp(1, _PathMultiplier, nearPath);
                clip(fade - 0.002);
                float2 wind = normalize(float2(0.94, 0.342));
                float2 drifted = samplePosition - wind * (_Time.y * _WindSpeed);
                float2 noisePosition = float2(dot(drifted, wind) / _NoiseStretch,
                    dot(drifted, float2(-wind.y, wind.x))) * _NoiseScale;
                float coarse = FogNoise(noisePosition);
                float fine = FogNoise(noisePosition * 2.7 + float2(_Time.y * 0.035, -_Time.y * 0.025) + 17.43);
                float banks = smoothstep(lerp(0.22, 0.18, _WaterLayer), lerp(0.72, 0.68, _WaterLayer),
                    coarse * 0.65 + fine * 0.35);
                half alpha = saturate(banks * _Density * _BaseColor.a * fade);
                clip(alpha - 0.002h);
                Light moon = GetMainLight();
                half3 color = _BaseColor.rgb + SampleSH(half3(0, 1, 0)) * 0.25h
                    + moon.color * moon.distanceAttenuation * _MoonStrength;
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0u; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    color += FogLighting(lightIndex, input.positionWS);
                }
                #endif
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    color += FogLighting(lightIndex, input.positionWS);
                LIGHT_LOOP_END
                #endif
                return half4(MixFog(min(color, 0.9h), input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
