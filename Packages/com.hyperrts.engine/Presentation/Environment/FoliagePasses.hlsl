// HyperRTS/Foliage vertex passes. All place vertices with FoliageInput so every pass sees the same plant.
#ifndef HYPERRTS_FOLIAGE_PASSES_INCLUDED
#define HYPERRTS_FOLIAGE_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/FoliageInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/PlantShading.hlsl"

ForwardVaryings ForwardVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), origin);
    half random = WindHash(float3(origin.x, 0.0, origin.z));
    half dry = saturate(DryPatch(origin.xz) * 0.65 + random * 0.35);
    half4 tint = half4(PlantTint(dry, random), lerp(1.0, input.color.r, _VertexOcclusion));
    half3 normal = TransformObjectToWorldNormal(input.normalOS);
    ForwardVaryings output = PlantForwardVaryings(world, origin, input.uv, input.uv.y, normal, tint);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

CutoutVaryings CutoutVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float4 positionCS = TransformWorldToHClip(FoliagePosition(input, WindThisFrame(), origin));
    CutoutVaryings output = PlantCutoutVaryings(positionCS, input.uv, TransformObjectToWorldNormal(input.normalOS));
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

CutoutVaryings ShadowVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), origin);
    half3 normal = TransformObjectToWorldNormal(input.normalOS);
    CutoutVaryings output = PlantCutoutVaryings(ShadowCasterPositionCS(world, normal), input.uv, normal);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

MotionVaryings MotionVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 placed = PlaceFoliage(input, origin);
    float3 now = SwayFoliage(input, placed, origin, WindThisFrame());
    float3 last = SwayFoliage(input, placed, origin, WindLastFrame());
    MotionVaryings output = PlantMotionVaryings(now, last, input.uv);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

#endif
