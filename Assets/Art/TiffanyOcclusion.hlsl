#ifndef TIFFANY_OCCLUSION_INCLUDED
#define TIFFANY_OCCLUSION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#ifndef REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
    #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#endif
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

float4 _TiffanyFocusWS;
float4 _TiffanyCameraWS;
float4 _TiffanyOcclusionParams;
static const float TiffanyBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float TiffanyKeepFraction(float3 point)
{
    float radius = _TiffanyFocusWS.w;
    float3 toFocus = _TiffanyFocusWS.xyz - _TiffanyCameraWS.xyz;
    float distance = length(toFocus);
    if (radius <= 0 || distance <= 0.0001) return 1;
    float3 axis = toFocus / distance;
    float3 offset = point - _TiffanyCameraWS.xyz;
    float along = dot(offset, axis);
    if (along <= _TiffanyOcclusionParams.w || along >= distance - _TiffanyOcclusionParams.y) return 1;
    float radial = length(offset - axis * along);
    float softness = _TiffanyOcclusionParams.x;
    float edge = saturate((radial - max(0, radius - softness)) / max(0.0001, softness));
    edge = edge * edge * (3 - 2 * edge);
    return lerp(_TiffanyOcclusionParams.z, 1, edge);
}

void TiffanyClipOccluder(float3 positionWS, float4 positionCS)
{
    #if !defined(_TIFFANY_CHARACTER)
        uint2 pixel = uint2(positionCS.xy) & 3u;
        clip(TiffanyKeepFraction(positionWS) - (TiffanyBayer4[pixel.y * 4u + pixel.x] + 0.5) / 16);
    #endif
}

void TiffanyForwardFragment(Varyings input, out half4 color : SV_Target0)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TiffanyClipOccluder(input.positionWS, input.positionCS);
    #if defined(_TIFFANY_UNLIT)
        half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
        half fog = InitializeInputDataFog(float4(input.positionWS, 1), 0);
        color = half4(MixFog(albedo, fog), 1);
    #else
        LitPassFragment(input, color);
    #endif
}

half TiffanyDepthFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TiffanyClipOccluder(input.positionWS, input.positionCS);
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return input.positionCS.z;
}

half4 TiffanyNormalsFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TiffanyClipOccluder(input.positionWS, input.positionCS);
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    InputData data;
    InitializeInputData(input, SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale), data);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 oct = PackNormalOctQuadEncode(data.normalWS);
        return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
    #else
        return half4(data.normalWS, 0);
    #endif
}
#endif
