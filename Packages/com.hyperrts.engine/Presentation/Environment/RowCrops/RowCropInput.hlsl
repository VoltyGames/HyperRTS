// HyperRTS/RowCrop inputs shared by every pass. Plants come from _Instances by SV_InstanceID.
#ifndef HYPERRTS_ROW_CROP_INPUT_INCLUDED
#define HYPERRTS_ROW_CROP_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wind.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/RowCrops/RowCropInstance.hlsl"

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
    half _GroundBlend;
    half _GroundBlendHeight;
    half _CanopyShade;
CBUFFER_END

float _CanopyHeight; // RowCrop.height, set per draw

StructuredBuffer<RowCropInstance> _Instances;

// PlantShading.hlsl: the side in view is lit, bent skyward so a field reads lit from above.
#define PLANT_TWO_SIDED 1.0

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
};

// Placed by its plant, leaned and trampled, before wind.
float3 PlaceRowCrop(Attributes input, RowCropInstance plant)
{
    float3 origin = plant.position;
    float3 world = origin + LeanOffset(PlantToWorld(input.positionOS.xyz * plant.scale, plant.facing), plant.lean);
    return TrampleBend(world, origin, plant.trampleDirection, plant.bend);
}

// Wind.hlsl plant mode, always at full scale.
float3 SwayRowCrop(float3 placed, RowCropInstance plant, WindFrame frame)
{
    return PlantWindPosition(placed, plant.position, _WindStrength, _WindSpeed, _Flutter, 1.0, frame);
}

float3 RowCropPosition(Attributes input, RowCropInstance plant, WindFrame frame)
{
    return SwayRowCrop(PlaceRowCrop(input, plant), plant, frame);
}

// Light left under the field's heads: it falls off fast below them, so rows show dark gaps between lit tops.
half CanopyLight(float height)
{
    half depth = (half)saturate(height / _CanopyHeight);
    return lerp(1.0 - _CanopyShade, 1.0, depth * depth);
}

half3 RowCropNormal(Attributes input, RowCropInstance plant)
{
    return normalize(PlantToWorld(input.normalOS, plant.facing));
}

#endif
