// HyperRTS/Foliage inputs shared by every pass.
#ifndef HYPERRTS_FOLIAGE_INPUT_INCLUDED
#define HYPERRTS_FOLIAGE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wind.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _Smoothness;
    half _Translucency;
    half _HueVariation;
    float _WindStrength;
    float _WindSpeed;
    float _Flutter;
    float _WindScale;
    float _BranchSway;
    float _CardFlutter;
    float _TreeHeight;
    half _GroundBlend;
    half _GroundBlendHeight;
    half _VertexOcclusion;
    float _Trample; // 0 off, 1 grass (flatten), 2 crop (damage)
CBUFFER_END

// x start, y end of plant shrinking (m from camera). Set by DetailFade, zero disables it.
float4 _HyperRTSDetailFade;

float3 PlantTrample(float3 world, float3 origin)
{
    TrampleSample trample = SampleTrample(origin.xz);
    half amount = _Trample > 1.5 ? trample.crop : trample.flatten;
    amount = _Trample < 0.5 ? 0.0 : amount;
    return TrampleBend(world, origin, trample.direction, amount);
}

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
#if defined(_TREE_WIND)
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float2 uv4 : TEXCOORD4;
#endif
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Motion vectors reuse the current object matrix: vegetation never moves.
float3 FoliagePosition(Attributes input, WindFrame frame, out float3 origin)
{
    origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

#if defined(_TREE_WIND)
    TreeWindSettings settings;
    settings.trunkSway = _WindStrength;
    settings.speed = _WindSpeed;
    settings.branchSway = _BranchSway;
    settings.cardFlutter = _CardFlutter;
    settings.height = _TreeHeight;
    settings.scale = _WindScale;
    TreeWindVertex v = TreeWindData(input.positionOS.xyz, input.uv2, input.uv3, input.uv4, input.color);
    return TransformObjectToWorld(TreeWindPosition(v, settings, frame));
#else
    float range = _HyperRTSDetailFade.y - _HyperRTSDetailFade.x;
    float fade = range > 0.0 ? saturate((_HyperRTSDetailFade.y - distance(origin, _WorldSpaceCameraPos)) / range) : 1.0;
    float3 world = TransformObjectToWorld(input.positionOS.xyz);
    world = PlantTrample(origin + (world - origin) * fade, origin);
    return PlantWindPosition(world, origin, _WindStrength, _WindSpeed, _Flutter, _WindScale, frame);
#endif
}

half Coverage(float2 uv)
{
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
    clip(alpha - _Cutoff);
    return alpha;
}

// LOD crossfade dither. positionCS is the fragment's SV_POSITION.
void FoliageCrossFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(positionCS);
#endif
}

#endif
