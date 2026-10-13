// HyperRTS/RowCrop inputs shared by every pass. Plants come from _Instances by SV_InstanceID.
#ifndef HYPERRTS_ROW_CROP_INPUT_INCLUDED
#define HYPERRTS_ROW_CROP_INPUT_INCLUDED

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
    half _GroundBlend;
    half _GroundBlendHeight;
CBUFFER_END

struct RowCropInstance // RowCrops.compute, 48 bytes
{
    float3 position;
    float scale;
    float2 facing;
    float2 trampleDirection;
    float bend;
    float shade;
    float2 padding;
};

StructuredBuffer<RowCropInstance> _Instances;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
};

// Object +Z faces the plant's way, +X to its right, +Y up.
float3 PlantToWorld(float3 v, float2 facing)
{
    float3 forward = float3(facing.x, 0.0, facing.y);
    float3 right = float3(facing.y, 0.0, -facing.x);
    return right * v.x + float3(0.0, v.y, 0.0) + forward * v.z;
}

// Placed by its plant, trampled, then swayed (Wind.hlsl plant mode).
float3 RowCropPosition(Attributes input, RowCropInstance plant, WindFrame frame)
{
    float3 origin = plant.position;
    float3 world = origin + PlantToWorld(input.positionOS.xyz * plant.scale, plant.facing);
    world = TrampleBend(world, origin, plant.trampleDirection, plant.bend);
    return PlantWindPosition(world, origin, _WindStrength, _WindSpeed, _Flutter, _WindScale, frame);
}

half3 RowCropNormal(Attributes input, RowCropInstance plant)
{
    return normalize(PlantToWorld(input.normalOS, plant.facing));
}

half Coverage(float2 uv)
{
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
    clip(alpha - _Cutoff);
    return alpha;
}

#endif
