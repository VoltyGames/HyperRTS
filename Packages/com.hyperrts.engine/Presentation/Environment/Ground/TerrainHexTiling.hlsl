// HyperRTS/Terrain hex tiling of the layer arrays: albedo, normal and far samples over the hex grid.
#ifndef HYPERRTS_TERRAIN_HEX_TILING_INCLUDED
#define HYPERRTS_TERRAIN_HEX_TILING_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/HexGrid.hlsl"

// (cos, sin) of the far sample's fixed turn, so its repeats don't line up with the near ones.
#define FAR_TURN float2(0.79863551, 0.60181502)

// Taller texels win where cells meet, so stones and tufts stay whole; the weights it leaves serve the other maps.
half4 SampleHexAlbedo(inout TerrainHex hex)
{
    float3 weights = 0.0;
    half4 albedo = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > HEX_MIN_WEIGHT)
        {
            half4 texel = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, cell.uv, hex.slice,
                cell.dx, cell.dy);
            weights[i] = hex.weights[i] * lerp(1.0, texel.a, HEX_HEIGHT_CONTRAST);
            albedo += weights[i] * texel;
        }
    }

    float total = weights.x + weights.y + weights.z;
    hex.weights = weights / total;
    return albedo / total;
}

half4 SampleHex(TEXTURE2D_ARRAY_PARAM(layers, samplerLayers), TerrainHex hex)
{
    half4 value = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > 0.0)
        {
            value += hex.weights[i] * SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, cell.uv, hex.slice,
                cell.dx, cell.dy);
        }
    }

    return value;
}

// Each cell's normal turned back by its cell's rotation into layer UV space.
half3 SampleHexNormal(TerrainHex hex, half strength)
{
    half3 normal = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > 0.0)
        {
            half4 packed = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, cell.uv, hex.slice,
                cell.dx, cell.dy);
            half3 cellNormal = UnpackNormalScale(packed, strength);
            cellNormal.xy = RotateUV(cellNormal.xy, cell.rotation);
            normal += hex.weights[i] * cellNormal;
        }
    }

    return normalize(normal);
}

// A coarser, turned copy of the layer's albedo: no hex, as it only shows at a distance.
half3 SampleFarAlbedo(float2 st, float2 dx, float2 dy, float slice)
{
    float2 uv = RotateUV(st, FAR_TURN) * _FarTiling;
    float2 uvDx = RotateUV(dx, FAR_TURN) * _FarTiling;
    float2 uvDy = RotateUV(dy, FAR_TURN) * _FarTiling;
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv, slice, uvDx, uvDy).rgb;
}

#endif
