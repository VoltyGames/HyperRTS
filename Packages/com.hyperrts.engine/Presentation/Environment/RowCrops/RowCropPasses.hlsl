// HyperRTS/RowCrop vertex passes, shaded like HyperRTS/Foliage (PlantShading.hlsl). All place with RowCropInput.
#ifndef HYPERRTS_ROW_CROP_PASSES_INCLUDED
#define HYPERRTS_ROW_CROP_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/RowCrops/RowCropInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/PlantShading.hlsl"

ForwardVaryings ForwardVert(Attributes input)
{
    RowCropInstance plant = _Instances[input.instanceID];
    float3 world = RowCropPosition(input, plant, WindThisFrame());
    float height = input.positionOS.y * plant.scale;
    half4 tint = half4(PlantTint(plant.shade, plant.shade), CanopyLight(height));
    return PlantForwardVaryings(world, plant.position, input.uv, height, RowCropNormal(input, plant), tint);
}

CutoutVaryings CutoutVert(Attributes input)
{
    RowCropInstance plant = _Instances[input.instanceID];
    float4 positionCS = TransformWorldToHClip(RowCropPosition(input, plant, WindThisFrame()));
    return PlantCutoutVaryings(positionCS, input.uv, RowCropNormal(input, plant));
}

CutoutVaryings ShadowVert(Attributes input)
{
    RowCropInstance plant = _Instances[input.instanceID];
    float3 world = RowCropPosition(input, plant, WindThisFrame());
    half3 normal = RowCropNormal(input, plant);
    return PlantCutoutVaryings(PlantShadowPosition(world, normal), input.uv, normal);
}

MotionVaryings MotionVert(Attributes input)
{
    RowCropInstance plant = _Instances[input.instanceID];
    float3 placed = PlaceRowCrop(input, plant);
    float3 now = SwayRowCrop(placed, plant, WindThisFrame());
    float3 last = SwayRowCrop(placed, plant, WindLastFrame());
    return PlantMotionVaryings(now, last, input.uv);
}

#endif
