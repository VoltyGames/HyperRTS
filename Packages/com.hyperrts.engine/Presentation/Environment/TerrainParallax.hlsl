// HyperRTS/Terrain parallax occlusion near the camera, marched on the dominant layer's heaviest hex cell. One xz offset
// then shifts every layer, so the height blend and hex cells stay consistent.
#ifndef HYPERRTS_TERRAIN_PARALLAX_INCLUDED
#define HYPERRTS_TERRAIN_PARALLAX_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayerParams.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainPixel.hlsl"

#define PARALLAX_STEPS 8
#define PARALLAX_FADE 10.0
// Caps the offset at grazing views to about 2.7 times the depth.
#define PARALLAX_MIN_COSINE 0.35

TerrainHexCell HeaviestCell(TerrainHex hex)
{
    float3 w = hex.weights;
    if (w.x >= w.y && w.x >= w.z)
    {
        return hex.cells[0];
    }

    if (w.y >= w.z)
    {
        return hex.cells[1];
    }

    return hex.cells[2];
}

half ParallaxHeight(TerrainHexCell cell, float slice, float2 uv)
{
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv, slice, cell.dx, cell.dy).a;
}

// Where the view ray first meets the layer's height, as a fraction of the way down, refined between the last steps.
float ParallaxHit(TerrainHexCell cell, float slice, float2 reach)
{
    float t = 0.0;
    float gap = 1.0 - ParallaxHeight(cell, slice, cell.uv);
    float previousT = 0.0;
    float previousGap = gap;
    [loop] for (uint i = 0; i < PARALLAX_STEPS && gap > 0.0; i++)
    {
        previousT = t;
        previousGap = gap;
        t += 1.0 / PARALLAX_STEPS;
        gap = 1.0 - ParallaxHeight(cell, slice, cell.uv + reach * t) - t;
    }

    return lerp(previousT, t, saturate(previousGap / max(previousGap - gap, 1e-5)));
}

// The xz shift (m) of what this pixel shows, centred on mid height so the ground doesn't swim against roads and units.
float2 ParallaxOffset(TerrainPixel pixel, uint layer)
{
    half fade = saturate((_ParallaxEnd - pixel.distance) / PARALLAX_FADE);
    float2 offset = 0.0;
    [branch] if (fade > 0.0)
    {
        TerrainLayerParams params = LoadLayerParams(layer, pixel.rowTurn);
        float2 st = RotateAndTile(pixel.xz, params);
        TerrainHex hex = GetHex(st, RotateAndTile(pixel.dx, params), RotateAndTile(pixel.dy, params), layer,
            params.hex);
        TerrainHexCell cell = HeaviestCell(hex);
        float3 view = mul(pixel.frame, normalize(GetCameraPositionWS() - pixel.positionWS));
        float2 reach = -view.xy / max(view.z, PARALLAX_MIN_COSINE) * _ParallaxDepth * fade;
        float2 reachXZ = (pixel.frame[0] * reach.x + pixel.frame[1] * reach.y).xz;
        float2 reachCell = RotateUV(RotateAndTile(reachXZ, params), float2(cell.rotation.x, -cell.rotation.y));
        offset = (ParallaxHit(cell, layer, reachCell) - 0.5) * reachXZ;
    }

    return offset;
}

#endif
