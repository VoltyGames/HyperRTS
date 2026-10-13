// HyperRTS/Foliage inputs shared by every pass: material properties, vertex attributes and the windblown position.
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
    float _FadeStart;
    float _FadeEnd;
    float _Trample; // 0 off, 1 grass (flatten), 2 crop (damage)
CBUFFER_END

// Leans a plant flat where it was trampled: grass by the flatten that recovers, crops by the damage that doesn't.
float3 PlantTrample(float3 world, float3 origin)
{
    UNITY_BRANCH
    if (_Trample < 0.5)
    {
        return world;
    }

    TrampleSample trample = SampleTrample(origin.xz);
    half amount = _Trample > 1.5 ? trample.crop : trample.flatten;
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

// World position of a vertex under the given frame's wind. The plant shrinks to its root between the fade distances
// (so terrain details are gone before the terrain culls them). Motion vectors reuse the current object matrix for
// last frame: vegetation doesn't move.
float3 FoliagePosition(Attributes input, WindFrame frame, out float3 origin)
{
    origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float fade = saturate((_FadeEnd - distance(origin, _WorldSpaceCameraPos)) / max(_FadeEnd - _FadeStart, 1e-3));

#if defined(_TREE_WIND)
    TreeWindSettings settings;
    settings.trunkSway = _WindStrength;
    settings.speed = _WindSpeed;
    settings.branchSway = _BranchSway;
    settings.cardFlutter = _CardFlutter;
    settings.height = _TreeHeight;
    settings.scale = _WindScale;
    TreeWindVertex v = TreeWindData(input.positionOS.xyz, input.uv2, input.uv3, input.uv4, input.color);
    float3 world = TransformObjectToWorld(TreeWindPosition(v, settings, frame));
    return origin + (world - origin) * fade;
#else
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

// Dithers between LOD levels while a LOD group crossfades; positionCS is the fragment's SV_POSITION.
void FoliageCrossFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(positionCS);
#endif
}

#endif
