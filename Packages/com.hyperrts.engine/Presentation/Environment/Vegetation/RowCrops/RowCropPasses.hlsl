// HyperRTS/RowCrop vertex passes, shaded like HyperRTS/Foliage (PlantShading.hlsl). All place with RowCropInput.
#ifndef HYPERRTS_ROW_CROP_PASSES_INCLUDED
#define HYPERRTS_ROW_CROP_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantShading.hlsl"

ForwardVaryings ForwardVert(Attributes input)
{
    RowCropInstance plant = _Instances[_SlotOffset + input.instanceID];
    float3 world = RowCropPosition(input, plant, WindThisFrame());
    float height = input.positionOS.y * plant.scale;
    half4 tint = half4(PlantTint(plant.shade, plant.shade), CanopyLight(height));
    half3 normal = PlantWindNormal(RowCropNormal(input, plant), plant.position, _WindStrength, _WindSpeed, 1.0,
        WindThisFrame());
    return PlantForwardVaryings(world, plant.position, input.uv, height, normal, tint);
}

// Instances are placed in world space, so the object matrix plays no part.
void Displace(Attributes input, WindFrame frame, float4x4 objectToWorld, out float3 positionWS, out half3 normalWS,
    out float2 uv)
{
    RowCropInstance plant = _Instances[_SlotOffset + input.instanceID];
    positionWS = RowCropPosition(input, plant, frame);
    normalWS = RowCropNormal(input, plant);
    uv = TRANSFORM_TEX(input.uv, _BaseMap);
}

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedPasses.hlsl"

#endif
