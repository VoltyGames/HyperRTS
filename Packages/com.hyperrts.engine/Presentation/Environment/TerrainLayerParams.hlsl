// HyperRTS/Terrain per-layer parameters, read from the _LayerParams columns documented in Terrain.shader.
#ifndef HYPERRTS_TERRAIN_LAYER_PARAMS_INCLUDED
#define HYPERRTS_TERRAIN_LAYER_PARAMS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainHexTiling.hlsl"

struct TerrainLayerParams
{
    float2 rotation;   // (cos, sin) of the clockwise UV rotation
    float2 tiling;     // 1 / tile size (m) along u and v
    half3 tint;
    half tintStrength;
    half4 mask;        // smoothness scale, offset; occlusion scale, offset
    half heightWeight;
    half normalStrength;
    half grass;        // grass cover, which sheds rain
    half triplanar;    // 1 for the slope layer, projected sideways on steep ground
    float4 hex;        // x hex rotation (fraction of a turn), y 0 for row layers else 1, zw row line direction in UV
};

TerrainLayerParams LoadLayerParams(uint layer, float2 rowTurn)
{
    float4 mapping = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 0));
    float4 colour = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1));
    float4 surface = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 3));
    float4 cover = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 4));
    TerrainLayerParams params;
    params.rotation = mapping.xy;
    params.tiling = mapping.zw;
    params.tint = colour.rgb;
    params.tintStrength = colour.a;
    params.mask = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 2));
    params.heightWeight = surface.x;
    params.normalStrength = surface.y;
    params.grass = cover.x;
    params.triplanar = cover.y;
    params.hex = float4(surface.zw, 1.0, 0.0);
    // Row layers add the field's turn to their own, so their lines run along each field's rows. Their own turn
    // of u, tiled, is where the lines lie in UV, and the only way hex cells may slide.
    [flatten] if (params.hex.y == 0.0)
    {
        params.hex.zw = normalize(params.rotation * params.tiling);
        params.rotation = RotateUV(params.rotation, rowTurn);
    }

    return params;
}

float2 RotateAndTile(float2 xz, TerrainLayerParams params)
{
    return RotateUV(xz, params.rotation) * params.tiling;
}

#endif
