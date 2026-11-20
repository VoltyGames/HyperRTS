// HyperRTS/Terrain per-pixel inputs shared by every layer: position, gradients, the terrain frame and distance fades.
#ifndef HYPERRTS_TERRAIN_PIXEL_INCLUDED
#define HYPERRTS_TERRAIN_PIXEL_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Triplanar.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainSurface.hlsl"

// The row map holds the rows' direction (x east, y north); u then runs along it. Unturned off the fields.
float2 RowTurn(float2 uv)
{
    float2 along = SAMPLE_TEXTURE2D_LOD(_RowMap, sampler_LinearClamp, uv, 0).rg * 2.0 - 1.0;
    float lengthSq = dot(along, along);
    along = lengthSq < 0.01 ? float2(1.0, 0.0) : along * rsqrt(max(lengthSq, 1e-6));
    return float2(along.x, -along.y);
}

// Sharpened triplanar weights, faded in from _TriplanarStart degrees of slope, so gentle ground stays top-projected.
void SideShares(inout TerrainPixel pixel, half3 normalWS)
{
    half3 weights = TriplanarWeights(normalWS);
    half slope = degrees(acos(saturate(normalWS.y)));
    half steep = saturate((slope - _TriplanarStart) / (_TriplanarFull - _TriplanarStart));
    half sides = weights.x + weights.z;
    pixel.side = steep * sides;
    pixel.sideWeights = weights.xz / max(sides, 1e-4);
}

TerrainPixel GetTerrainPixel(float2 uv, float3 positionWS, half3 normalWS)
{
    TerrainPixel pixel;
    pixel.positionWS = positionWS;
    pixel.dPdx = ddx(positionWS);
    pixel.dPdy = ddy(positionWS);
    pixel.xz = positionWS.xz;
    pixel.dx = pixel.dPdx.xz;
    pixel.dy = pixel.dPdy.xz;
    pixel.frame = TerrainFrame(normalWS);
    pixel.distance = length(positionWS - GetCameraPositionWS());
    pixel.far = saturate((pixel.distance - _FarStart) / (_FarEnd - _FarStart)) * _FarBlend;
    pixel.relief = lerp(1.0, _FarRelief, saturate((pixel.distance - _ReliefStart) / (_ReliefEnd - _ReliefStart)));
    pixel.rowTurn = RowTurn(uv);
    SideShares(pixel, normalWS);
    return pixel;
}

#endif
