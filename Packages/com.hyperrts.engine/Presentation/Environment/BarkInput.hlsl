// HyperRTS/Bark inputs: a cut-down URP Lit material (base, normal, optional mask map) plus the tree wind settings that
// the tree's Foliage leaves share. Every Bark pass includes this, so the SRP Batcher sees one UnityPerMaterial layout.
#ifndef HYPERRTS_BARK_INPUT_INCLUDED
#define HYPERRTS_BARK_INPUT_INCLUDED
// URP's pass files include "LitInput.hlsl" themselves; its guard keeps this file's declarations the only ones.
#define UNIVERSAL_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
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

// HDRP layout: R metallic, G occlusion, A smoothness.
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

// Tree mesh vertex with its wind data (Wind.hlsl); URP Lit's own Attributes would read TEXCOORD1/2 as lightmap UVs.
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

// Object-space position under the given frame's wind; bark has no cards, so only the trunk and branches move it.
float3 BarkWindPosition(BarkAttributes input, WindFrame frame)
{
    TreeWindSettings settings;
    settings.trunkSway = _WindStrength;
    settings.speed = _WindSpeed;
    settings.branchSway = _BranchSway;
    settings.cardFlutter = 0.0;
    settings.height = _TreeHeight;
    settings.scale = _WindScale;
    TreeWindVertex v = TreeWindData(input.positionOS.xyz, input.uv2, input.uv3, input.uv4, input.color);
    return TreeWindPosition(v, settings, frame);
}

// URP's pass files call this by name; bark fills it from its own maps.
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedo = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
    outSurfaceData.albedo = albedo.rgb;
    outSurfaceData.alpha = 1.0;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

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
