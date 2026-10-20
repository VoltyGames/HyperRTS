// HyperRTS/Terrain layer blending: an online softmax over log(weight) + height / depth, so the tallest texels win
// where layers mix without knowing every layer's height up front.
#ifndef HYPERRTS_TERRAIN_LAYERS_INCLUDED
#define HYPERRTS_TERRAIN_LAYERS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainHexTiling.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"

#define TERRAIN_MAX_LAYERS 16
#define TERRAIN_MIN_WEIGHT 0.003
// Layers with less of the blend so far skip their normal and mask samples.
#define TERRAIN_MIN_SHARE 0.02

struct TerrainLayerParams
{
    float2 rotation;   // (cos, sin) of the clockwise UV rotation
    float2 tiling;     // 1 / tile size (m) along u and v
    half3 tint;
    half macro;
    half4 mask;        // smoothness scale, offset; occlusion scale, offset
    half heightWeight;
    half normalStrength;
    half grass;        // grass cover, which sheds rain
    float4 hex;        // x hex rotation (fraction of a turn), y 0 for row layers else 1, zw row line direction in UV
};

struct TerrainPixel
{
    float2 xz;
    float2 dx;
    float2 dy;
    half far;          // the far sample's share of albedo
    float2 rowTurn;    // (cos, sin) that lays u along this pixel's crop rows
};

struct TerrainBlend
{
    float maxScore;
    float total;
    float3 albedo;
    float3 normal;
    float smoothness;
    float occlusion;
    float height;
    float macro;
    float grass;
};

struct TerrainSurface
{
    half3 albedo;
    half3 normalTS;    // terrain tangent space: x east, y north, z along the terrain normal
    half smoothness;
    half occlusion;
    half height;
    half macro;
    half grass;
};

TerrainLayerParams LoadLayerParams(uint layer, float2 rowTurn)
{
    float4 mapping = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 0));
    float4 colour = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1));
    float4 surface = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 3));
    TerrainLayerParams params;
    params.rotation = mapping.xy;
    params.tiling = mapping.zw;
    params.tint = colour.rgb;
    params.macro = colour.a;
    params.mask = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 2));
    params.heightWeight = surface.x;
    params.normalStrength = surface.y;
    params.grass = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 4)).x;
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

// The layer's hex-tiled albedo, with the far sample at st mixed in by camera distance.
half4 LayerAlbedo(inout TerrainHex hex, float2 st, float2 dx, float2 dy, half far)
{
    half4 albedo = SampleHexAlbedo(hex);
    #ifdef TERRAIN_FAR_SAMPLE
        [branch] if (far > 0.0)
        {
            albedo.rgb = lerp(albedo.rgb, SampleFarAlbedo(st, dx, dy, hex.slice), far);
        }
    #endif
    return albedo;
}

// The layer's normal turned back by its UV rotation into terrain tangent space.
half3 LayerNormal(TerrainHex hex, TerrainLayerParams params)
{
    half3 normal = SampleHexNormal(hex, params.normalStrength);
    return half3(RotateUV(normal.xy, params.rotation * float2(1.0, -1.0)), normal.z);
}

void FadeBlend(inout TerrainBlend blend, float keep)
{
    blend.total *= keep;
    blend.albedo *= keep;
    blend.normal *= keep;
    blend.smoothness *= keep;
    blend.occlusion *= keep;
    blend.height *= keep;
    blend.macro *= keep;
    blend.grass *= keep;
}

void AddLayer(inout TerrainBlend blend, uint layer, half weight, TerrainPixel pixel)
{
    TerrainLayerParams params = LoadLayerParams(layer, pixel.rowTurn);
    // Explicit gradients: hex cells jump at their edges and layers are sampled inside branches.
    float2 st = RotateAndTile(pixel.xz, params);
    float2 dx = RotateAndTile(pixel.dx, params);
    float2 dy = RotateAndTile(pixel.dy, params);
    TerrainHex hex = GetHex(st, dx, dy, layer, params.hex);
    half4 albedo = LayerAlbedo(hex, st, dx, dy, pixel.far);

    // Share = weight x e^(height / depth): equal heights keep the painted blend, taller texels win where layers mix.
    float score = log(weight) + params.heightWeight * (albedo.a - 0.5) / _HeightBlendDepth;
    float maxScore = max(blend.maxScore, score);
    FadeBlend(blend, exp(blend.maxScore - maxScore));
    blend.maxScore = maxScore;
    // Ramped in from the weight threshold, so a tall layer doesn't pop in at its edge.
    float share = exp(score - maxScore) * saturate(weight * 50.0);
    blend.total += share;
    blend.albedo += share * albedo.rgb * params.tint;
    blend.height += share * albedo.a;
    blend.macro += share * params.macro;
    blend.grass += share * params.grass;

    half3 normal = half3(0.0, 0.0, 1.0);
    half2 mask = half2(1.0, 0.0);
    if (share > blend.total * TERRAIN_MIN_SHARE)
    {
        normal = LayerNormal(hex, params);
        mask = SampleHex(TEXTURE2D_ARRAY_ARGS(_MaskArray, sampler_AlbedoArray), hex).rg;
    }

    blend.normal += share * normal;
    blend.occlusion += share * (mask.r * params.mask.z + params.mask.w);
    blend.smoothness += share * (mask.g * params.mask.x + params.mask.y);
}

half LayerWeight(half4 control0, half4 control1, half4 control2, half4 control3, uint layer)
{
    half4 control = layer < 4 ? control0 : layer < 8 ? control1 : layer < 12 ? control2 : control3;
    return control[layer & 3];
}

TerrainSurface ResolveBlend(TerrainBlend blend)
{
    float scale = 1.0 / max(blend.total, 1e-6);
    TerrainSurface surface;
    surface.albedo = blend.albedo * scale;
    surface.normalTS = normalize(blend.normal + float3(0.0, 0.0, 1e-5));
    surface.smoothness = blend.smoothness * scale;
    surface.occlusion = blend.occlusion * scale;
    surface.height = blend.height * scale;
    surface.macro = blend.macro * scale;
    surface.grass = blend.grass * scale;
    return surface;
}

// The row map holds the rows' direction (x east, y north); u then runs along it. Unturned off the fields.
float2 RowTurn(float2 uv)
{
    float2 along = SAMPLE_TEXTURE2D_LOD(_RowMap, sampler_LinearClamp, uv, 0).rg * 2.0 - 1.0;
    float lengthSq = dot(along, along);
    along = lengthSq < 0.01 ? float2(1.0, 0.0) : along * rsqrt(max(lengthSq, 1e-6));
    return float2(along.x, -along.y);
}

TerrainPixel GetTerrainPixel(float2 uv, float3 positionWS)
{
    TerrainPixel pixel;
    pixel.xz = positionWS.xz;
    pixel.dx = ddx(pixel.xz);
    pixel.dy = ddy(pixel.xz);
    float cameraDistance = length(positionWS - GetCameraPositionWS());
    pixel.far = saturate((cameraDistance - _FarStart) / (_FarEnd - _FarStart)) * _FarBlend;
    pixel.rowTurn = RowTurn(uv);
    return pixel;
}

TerrainSurface BlendLayers(float2 uv, float3 positionWS)
{
    float2 controlUV = ControlUV(uv);
    half4 control0 = SAMPLE_TEXTURE2D(_Control0, sampler_LinearClamp, controlUV);
    half4 control1 = SAMPLE_TEXTURE2D(_Control1, sampler_LinearClamp, controlUV);
    half4 control2 = SAMPLE_TEXTURE2D(_Control2, sampler_LinearClamp, controlUV);
    half4 control3 = SAMPLE_TEXTURE2D(_Control3, sampler_LinearClamp, controlUV);

    TerrainPixel pixel = GetTerrainPixel(uv, positionWS);
    TerrainBlend blend = (TerrainBlend)0;
    blend.maxScore = -1e8;
    uint count = min((uint)_LayerCount, TERRAIN_MAX_LAYERS);
    [loop] for (uint layer = 0; layer < count; layer++)
    {
        half weight = LayerWeight(control0, control1, control2, control3, layer);
        [branch] if (weight > TERRAIN_MIN_WEIGHT)
        {
            AddLayer(blend, layer, weight, pixel);
        }
    }

    return ResolveBlend(blend);
}

// The map's macro tint and world-noise variation (broad light and dark, smaller warm and cool patches), scaled by the
// layers' macro strength. Returns the macro's dampness for TerrainWetness.hlsl.
half ApplyMacro(inout TerrainSurface surface, float2 uv, float2 positionXZ)
{
    half4 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_LinearClamp, uv);
    half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LinearRepeat, positionXZ / _VariationSize).gb;
    half light = (noise.x - 0.5) * 2.0 * _Variation;
    half warmth = (noise.y - 0.5) * _Variation;
    half3 variation = 1.0 + half3(light + warmth, light, light - warmth);
    surface.albedo *= lerp(1.0, macro.rgb * 2.0 * variation, surface.macro);

    // The grass cards' dry and green patches, so grass and the ground under it change together.
    [branch] if (surface.grass * _GrassPatches != 0.0)
    {
        half patch = (half)(DryPatch(positionXZ) * 2.0 - 1.0) * _GrassPatches;
        surface.albedo *= lerp(1.0, 1.0 + patch * half3(1.0, 0.55, 0.15), surface.grass);
    }

    return macro.a;
}

#endif
