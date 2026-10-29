Shader "TiffanyGame/Spirit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.7490196, 0.8901961, 1, 1)
        _RimPower("Rim Power", Range(0.1, 8)) = 3
        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.5
        [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Smoothness("Smoothness", Float) = 0
        [HideInInspector] _Metallic("Metallic", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _EmissionMap("Emission Map", 2D) = "white" {}
        [HideInInspector] _TiffanyStencil("Character Stencil", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _RimColor;
            float4 _Color;
            float _RimPower;
            float _RimIntensity;
            float _Smoothness;
            float _Metallic;
            float _Surface;
            float _AlphaClip;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Stencil { Ref [_TiffanyStencil] ReadMask 1 WriteMask 1 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SpiritVertex
            #pragma fragment SpiritFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 vertexLighting : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings SpiritVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);

                #if defined(_ADDITIONAL_LIGHTS_VERTEX) || defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = position.positionWS;
                    inputData.normalizedScreenSpaceUV = clamp(position.positionNDC.xy / max(position.positionNDC.w, 0.00001),
                                                             0.0, 1.0 - rcp(GetScaledScreenParams().xy));
                    #if USE_CLUSTER_LIGHT_LOOP
                        for (uint i = 0u; i < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++i)
                        {
                            Light light = GetAdditionalLight(i, position.positionWS);
                            output.vertexLighting += LightingLambert(light.color * light.distanceAttenuation,
                                                                     light.direction, output.normalWS);
                        }
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, position.positionWS);
                        output.vertexLighting += LightingLambert(light.color * light.distanceAttenuation,
                                                                 light.direction, output.normalWS);
                    LIGHT_LOOP_END
                #endif
                return output;
            }

            half4 SpiritFragment(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half3 diffuse = LightingLambert(mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation,
                                               mainLight.direction, normal);
                half3 lit = _BaseColor.rgb * (max(SampleSH(normal), 0.0h) + diffuse + input.vertexLighting);
                half rim = pow(saturate(1.0h - saturate(dot(normal, view))), max(_RimPower, 0.001));
                half3 color = lit + _RimColor.rgb * (_RimIntensity * rim);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
