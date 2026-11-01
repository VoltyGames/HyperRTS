// HyperRTS/Flag's prepasses: the cloth from FlagCloth.hlsl through DisplacedPasses.hlsl.
#ifndef HYPERRTS_FLAG_PASSES_INCLUDED
#define HYPERRTS_FLAG_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Flags/FlagCloth.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wind.hlsl"

// Both sides of the cloth show, so each side's normal faces its viewer.
#define DISPLACED_FACING_NORMALS

void Displace(Attributes input, WindFrame frame, float4x4 objectToWorld, out float3 positionWS, out half3 normalWS,
    out float2 uv)
{
    Cloth cloth = FlagCloth(input.positionOS.xyz, input.uv, frame.time, objectToWorld);
    positionWS = cloth.positionWS;
    normalWS = cloth.normalWS;
    uv = input.uv;
}

// The cloth is opaque.
void ClipDisplaced(float2 uv, float4 positionCS)
{
}

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedPasses.hlsl"

#endif
