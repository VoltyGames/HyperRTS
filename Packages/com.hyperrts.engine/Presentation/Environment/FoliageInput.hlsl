// HyperRTS/Foliage inputs shared by every pass.
#ifndef HYPERRTS_FOLIAGE_INPUT_INCLUDED
#define HYPERRTS_FOLIAGE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wind.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"

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
    half _TwoSidedLeaves;
CBUFFER_END

// PlantShading.hlsl: modelled leaves light the side in view; cards and crowns light both sides alike.
#define PLANT_TWO_SIDED _TwoSidedLeaves

// x start, y end of plant shrinking (m from camera). Set by DetailFade, zero disables it.
float4 _HyperRTSDetailFade;

float3 PlantTrample(float3 world, float3 origin)
{
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

// Faded and trampled, before wind. Tree wind places nothing: it sways the object-space vertex.
float3 PlaceFoliage(Attributes input, out float3 origin)
{
    origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
#if defined(_TREE_WIND)
    return input.positionOS.xyz;
#else
    float range = _HyperRTSDetailFade.y - _HyperRTSDetailFade.x;
    float fade = range > 0.0 ? saturate((_HyperRTSDetailFade.y - distance(origin, _WorldSpaceCameraPos)) / range) : 1.0;
    float3 world = TransformObjectToWorld(input.positionOS.xyz);
    return PlantTrample(origin + (world - origin) * fade, origin);
#endif
}

// Motion vectors reuse the current object matrix: vegetation never moves.
float3 SwayFoliage(Attributes input, float3 placed, float3 origin, WindFrame frame)
{
#if defined(_TREE_WIND)
    TreeWindSettings settings = TreeWind(_WindStrength, _WindSpeed, _BranchSway, _CardFlutter, _TreeHeight, _WindScale);
    TreeWindVertex v = TreeWindData(placed, input.uv2, input.uv3, input.uv4, input.color);
    return TransformObjectToWorld(TreeWindPosition(v, settings, frame));
#else
    return PlantWindPosition(placed, origin, _WindStrength, _WindSpeed, _Flutter, _WindScale, frame);
#endif
}

float3 FoliagePosition(Attributes input, WindFrame frame, out float3 origin)
{
    float3 placed = PlaceFoliage(input, origin);
    return SwayFoliage(input, placed, origin, frame);
}

#endif
