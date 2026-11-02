// HyperRTS/Foliage vertex passes. All place vertices with FoliageInput so every pass sees the same plant.
#ifndef HYPERRTS_FOLIAGE_PASSES_INCLUDED
#define HYPERRTS_FOLIAGE_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/FoliageInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantShading.hlsl"

// Each leaf cluster of a tree a little lighter or darker, and a few drier, so a crown reads as many clumps.
half3 ClusterTint(Attributes input)
{
#if defined(_TREE_WIND)
    float3 cardPivot = float3(input.uv3.y, input.uv4.xy);
    half value = Random(cardPivot) * 2.0 - 1.0;
    half dry = saturate(Random(cardPivot + 17.0) * 2.0 - 1.4);
    half3 straw = lerp(half3(1.0, 1.0, 1.0), half3(1.1, 1.03, 0.75), dry);
    return straw * (1.0 + value * _ClusterVariation);
#else
    return half3(1.0, 1.0, 1.0);
#endif
}

// Straw-dry patches and per-plant shade; scattered grass also takes its plant's tints and the ground's patches.
half4 FoliageTint(Attributes input, float3 origin)
{
#if defined(_GRASS_INSTANCES)
    GrassInstance grass = GrassAt(input);
    half random = grass.shade;
#else
    half random = Random(float3(origin.x, 0.0, origin.z));
#endif
    half dry = saturate(DryPatch(origin.xz) * 0.65 + random * 0.35);
    half3 tint = PlantTint(dry, random) * ClusterTint(input);
#if defined(_GRASS_INSTANCES)
    tint *= lerp(_GrassHealthy.rgb, _GrassDry.rgb, dry) * grass.ground;
#endif
    return half4(tint, lerp(1.0, input.color.r, _VertexOcclusion));
}

ForwardVaryings ForwardVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), UNITY_MATRIX_M, origin);
    ForwardVaryings output = PlantForwardVaryings(world, origin, input.uv, input.uv.y, FoliageNormal(input),
        FoliageTint(input, origin));
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

void Displace(Attributes input, WindFrame frame, float4x4 objectToWorld, out float3 positionWS, out half3 normalWS,
    out float2 uv)
{
    float3 origin;
    positionWS = FoliagePosition(input, frame, objectToWorld, origin);
    normalWS = FoliageNormal(input);
    uv = TRANSFORM_TEX(input.uv, _BaseMap);
}

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedPasses.hlsl"

#endif
