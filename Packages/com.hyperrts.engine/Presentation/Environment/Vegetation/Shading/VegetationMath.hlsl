// Small math shared by wind, trample, plant shading and the terrain. Plain HLSL, no textures.
#ifndef HYPERRTS_VEGETATION_MATH_INCLUDED
#define HYPERRTS_VEGETATION_MATH_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

float3 WindSafeNormalize(float3 v)
{
    return v * rsqrt(max(dot(v, v), 1e-8));
}

// Rodrigues rotation of p about a unit axis through pivot.
float3 WindRotate(float3 p, float3 pivot, float3 axis, float angle)
{
    float3 v = p - pivot;
    float s, c;
    sincos(angle, s, c);
    return pivot + v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

// Object +Z faces the plant's way, +X to its right, +Y up.
float3 PlantToWorld(float3 v, float2 facing)
{
    float3 forward = float3(facing.x, 0.0, facing.y);
    float3 right = float3(facing.y, 0.0, -facing.x);
    return right * v.x + float3(0.0, v.y, 0.0) + forward * v.z;
}

// Shears height over by a few degrees: close enough to a turn and keeps the root in place.
float3 LeanOffset(float3 offset, float2 lean)
{
    float upright = sqrt(saturate(1.0 - dot(lean, lean)));
    return float3(offset.x + lean.x * offset.y, offset.y * upright, offset.z + lean.y * offset.y);
}

// Drier and greener patches: a 10 m and a 4.4 m octave, stretched towards the full 0..1 range.
static const float PatchCoarseCellsPerMetre = 1.0 / 10.0;
static const float PatchFineCellsPerMetre = 9.0 / 40.0;
static const float PatchFineWeight = 0.35;

float DryPatch(float2 positionXZ)
{
    float coarse = ValueNoise(positionXZ * PatchCoarseCellsPerMetre);
    float fine = ValueNoise(positionXZ * PatchFineCellsPerMetre);
    return saturate((lerp(coarse, fine, PatchFineWeight) - 0.5) * 1.6 + 0.5);
}

#endif
