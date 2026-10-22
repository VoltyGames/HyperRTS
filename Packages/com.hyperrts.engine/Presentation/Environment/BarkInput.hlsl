// HyperRTS/Bark inputs, shared by every pass so the SRP Batcher sees one UnityPerMaterial layout.
#ifndef HYPERRTS_BARK_INPUT_INCLUDED
#define HYPERRTS_BARK_INPUT_INCLUDED
// LitInput.hlsl's include guard keeps these declarations the only ones.
#define UNIVERSAL_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/NormalMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/DetailMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wind.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
float _WindStrength;
float _WindSpeed;
float _BranchSway;
float _TreeHeight;
float _WindScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
// HDRP mask layout: R metallic, G occlusion, A smoothness.
TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_BaseMap);

// URP's Utils headers and pass files declare these.
half4 GetBaseColor()            { return _BaseColor; }
half GetCutoff()                { return _Cutoff; }
half GetBumpScale()             { return _BumpScale; }
half4 SampleBaseMap(float2 uv)  { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv); }
half4 SampleBumpMap(float2 uv)  { return SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv); }

// Bark has no detail map, so LitDepthNormalsPass's calls behind UseDetailMap() compile away.
half4 SampleDetailMask(float2 uv);
float4 GetDetailAlbedoMapST();

// Own vertex struct: URP Lit's Attributes would read TEXCOORD1/2 as lightmap UVs.
struct BarkAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float2 uv4 : TEXCOORD4;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 BarkWindPosition(BarkAttributes input, WindFrame frame)
{
    TreeWindSettings settings = TreeWind(_WindStrength, _WindSpeed, _BranchSway, 0.0, _TreeHeight, _WindScale);
    TreeWindVertex v = TreeWindData(input.positionOS.xyz, input.uv2, input.uv3, input.uv4, input.color);
    return TreeWindPosition(v, settings, frame);
}

// URP's pass files call this by name.
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedo = SampleBaseMap(uv) * GetBaseColor();
    outSurfaceData.albedo = albedo.rgb;
    outSurfaceData.alpha = 1.0;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.normalTS = SampleNormal(uv);

#if defined(_MASKMAP)
    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
    outSurfaceData.metallic = mask.r * _Metallic;
    outSurfaceData.smoothness = mask.a * _Smoothness;
    outSurfaceData.occlusion = lerp(1.0, mask.g, _OcclusionStrength);
#else
    outSurfaceData.metallic = _Metallic;
    outSurfaceData.smoothness = _Smoothness;
    outSurfaceData.occlusion = 1.0;
#endif
}

#endif
