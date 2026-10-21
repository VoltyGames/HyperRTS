// HyperRTS/Foliage vertex passes. All place vertices with FoliageInput so every pass sees the same plant.
#ifndef HYPERRTS_FOLIAGE_PASSES_INCLUDED
#define HYPERRTS_FOLIAGE_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/FoliageInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/PlantShading.hlsl"

// Straw-dry patches and per-plant shade; scattered grass also takes its plant's tints and the ground's patches.
half4 FoliageTint(Attributes input, float3 origin)
{
#if defined(_GRASS_INSTANCES)
    GrassInstance grass = GrassAt(input);
    half random = grass.shade;
#else
    half random = WindHash(float3(origin.x, 0.0, origin.z));
#endif
    half dry = saturate(DryPatch(origin.xz) * 0.65 + random * 0.35);
    half3 tint = PlantTint(dry, random);
#if defined(_GRASS_INSTANCES)
    tint *= lerp(_GrassHealthy.rgb, _GrassDry.rgb, dry) * grass.ground;
#endif
    return half4(tint, lerp(1.0, input.color.r, _VertexOcclusion));
}

ForwardVaryings ForwardVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), origin);
    ForwardVaryings output = PlantForwardVaryings(world, origin, input.uv, input.uv.y, FoliageNormal(input),
        FoliageTint(input, origin));
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

CutoutVaryings CutoutVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float4 positionCS = TransformWorldToHClip(FoliagePosition(input, WindThisFrame(), origin));
    CutoutVaryings output = PlantCutoutVaryings(positionCS, input.uv, FoliageNormal(input));
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

CutoutVaryings ShadowVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), origin);
    half3 normal = FoliageNormal(input);
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
