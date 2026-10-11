// HyperRTS/Terrain layer blending. Each layer with alphamap weight is sampled at its rotated, tiled world UV, then
// all are blended by weight and texel height in one pass: an online softmax over log(weight) + height / depth, so
// the tallest texels win where layers mix without knowing every layer's height up front.
#ifndef HYPERRTS_TERRAIN_LAYERS_INCLUDED
#define HYPERRTS_TERRAIN_LAYERS_INCLUDED

#define TERRAIN_MAX_LAYERS 16
#define TERRAIN_MIN_WEIGHT 0.003
// Layers with less of the blend so far skip their normal and mask samples.
#define TERRAIN_MIN_SHARE 0.02
// Anti-tiling: offset copies per noise unit, how quickly one copy hands over to the next, and how much texel
// heights shape the handover.
#define ANTI_TILING_COPIES 8.0
#define ANTI_TILING_SHARPNESS 8.0
#define ANTI_TILING_HEIGHT 1.0

struct TerrainLayerParams
{
    float2 rotation;   // (cos, sin) of the clockwise UV rotation
    float2 tiling;     // 1 / tile size (m) along u and v
    half3 tint;
    half macro;
    half4 mask;        // smoothness scale, offset; occlusion scale, offset
    half heightWeight;
    half normalStrength;
    float2 antiTiling; // copy offset scale in tiles along u and v
};

struct TerrainAntiTiling
{
    float2 offsetA;
    float2 offsetB;
    half handover;     // copy B's share before texel heights shape it
    bool both;         // the handover band, where both copies can show
};

struct TerrainLayerUV
{
    float2 a;
    float2 b;
    float2 dx;
    float2 dy;
    float slice;
    bool both;
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
};

struct TerrainSurface
{
    half3 albedo;
    half3 normalTS;    // terrain tangent space: x east, y north, z along the terrain normal
    half smoothness;
    half occlusion;
    half height;
    half macro;
};

TerrainLayerParams LoadLayerParams(uint layer)
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
    params.antiTiling = surface.zw;
    return params;
}

float2 CopyOffset(float copy)
{
    return frac(sin(float2(copy * 12.9898, copy * 78.233)) * 43758.5453);
}

// Low-frequency world noise picks two neighbouring offset copies of every texture and the handover between them.
TerrainAntiTiling GetAntiTiling(float2 positionXZ)
{
    float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LinearRepeat, positionXZ / _AntiTilingSize).r;
    float copy = noise * ANTI_TILING_COPIES;
    TerrainAntiTiling antiTiling;
    antiTiling.offsetA = CopyOffset(floor(copy));
    antiTiling.offsetB = CopyOffset(floor(copy) + 1.0);
    antiTiling.handover = (frac(copy) - 0.5) * ANTI_TILING_SHARPNESS + 0.5;
    antiTiling.both = abs(antiTiling.handover - 0.5) < 0.5 + ANTI_TILING_HEIGHT;
    return antiTiling;
}

float2 RotateAndTile(float2 xz, TerrainLayerParams params)
{
    float2 r = params.rotation;
    return float2(xz.x * r.x - xz.y * r.y, xz.x * r.y + xz.y * r.x) * params.tiling;
}

TerrainLayerUV GetLayerUV(uint layer, TerrainLayerParams params, float2 xz, float2 dx, float2 dy,
    TerrainAntiTiling antiTiling)
{
    float2 uv = RotateAndTile(xz, params);
    TerrainLayerUV layerUV;
    layerUV.a = uv + antiTiling.offsetA * params.antiTiling;
    layerUV.b = uv + antiTiling.offsetB * params.antiTiling;
    // Explicit gradients: the copies jump at band edges and layers are sampled inside branches.
    layerUV.dx = RotateAndTile(dx, params);
    layerUV.dy = RotateAndTile(dy, params);
    layerUV.slice = layer;
    layerUV.both = antiTiling.both && any(params.antiTiling > 0.0);
    return layerUV;
}

// One sample where a single copy shows, two in the handover.
half4 SampleCopies(TEXTURE2D_ARRAY_PARAM(layers, samplerLayers), TerrainLayerUV uv, half handover)
{
    half4 copy;
    if (handover <= 0.0)
    {
        copy = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, uv.a, uv.slice, uv.dx, uv.dy);
    }
    else if (handover >= 1.0)
    {
        copy = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, uv.b, uv.slice, uv.dx, uv.dy);
    }
    else
    {
        half4 a = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, uv.a, uv.slice, uv.dx, uv.dy);
        half4 b = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, uv.b, uv.slice, uv.dx, uv.dy);
        copy = lerp(a, b, handover);
    }

    return copy;
}

// In the handover band the taller texels of either copy win, so stones and tufts stay whole.
half4 SampleAlbedo(TerrainLayerUV uv, TerrainAntiTiling antiTiling, out half handover)
{
    half4 albedo;
    if (uv.both)
    {
        half4 a = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv.a, uv.slice, uv.dx, uv.dy);
        half4 b = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv.b, uv.slice, uv.dx, uv.dy);
        handover = saturate(antiTiling.handover + (b.a - a.a) * ANTI_TILING_HEIGHT);
        albedo = lerp(a, b, handover);
    }
    else
    {
        handover = step(0.5, antiTiling.handover);
        albedo = SampleCopies(TEXTURE2D_ARRAY_ARGS(_AlbedoArray, sampler_AlbedoArray), uv, handover);
    }

    return albedo;
}

// The layer's normal turned back by its UV rotation into terrain tangent space.
half3 LayerNormal(TerrainLayerUV uv, half handover, TerrainLayerParams params)
{
    half4 packed = SampleCopies(TEXTURE2D_ARRAY_ARGS(_NormalArray, sampler_AlbedoArray), uv, handover);
    half3 normal = UnpackNormalScale(packed, params.normalStrength);
    float2 r = params.rotation;
    return half3(normal.x * r.x + normal.y * r.y, normal.y * r.x - normal.x * r.y, normal.z);
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
}

void AddLayer(inout TerrainBlend blend, uint layer, half weight, float2 xz, float2 dx, float2 dy,
    TerrainAntiTiling antiTiling)
{
    TerrainLayerParams params = LoadLayerParams(layer);
    TerrainLayerUV uv = GetLayerUV(layer, params, xz, dx, dy, antiTiling);
    half handover;
    half4 albedo = SampleAlbedo(uv, antiTiling, handover);

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

    half3 normal = half3(0.0, 0.0, 1.0);
    half2 mask = half2(1.0, 0.0);
    if (share > blend.total * TERRAIN_MIN_SHARE)
    {
        normal = LayerNormal(uv, handover, params);
        mask = SampleCopies(TEXTURE2D_ARRAY_ARGS(_MaskArray, sampler_AlbedoArray), uv, handover).rg;
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
    return surface;
}

TerrainSurface BlendLayers(float2 uv, float3 positionWS)
{
    float2 controlUV = ControlUV(uv);
    half4 control0 = SAMPLE_TEXTURE2D(_Control0, sampler_LinearClamp, controlUV);
    half4 control1 = SAMPLE_TEXTURE2D(_Control1, sampler_LinearClamp, controlUV);
    half4 control2 = SAMPLE_TEXTURE2D(_Control2, sampler_LinearClamp, controlUV);
    half4 control3 = SAMPLE_TEXTURE2D(_Control3, sampler_LinearClamp, controlUV);

    float2 xz = positionWS.xz;
    float2 dx = ddx(xz);
    float2 dy = ddy(xz);
    TerrainAntiTiling antiTiling = GetAntiTiling(xz);
    TerrainBlend blend = (TerrainBlend)0;
    blend.maxScore = -1e8;
    uint count = min((uint)_LayerCount, TERRAIN_MAX_LAYERS);
    [loop] for (uint layer = 0; layer < count; layer++)
    {
        half weight = LayerWeight(control0, control1, control2, control3, layer);
        [branch] if (weight > TERRAIN_MIN_WEIGHT)
        {
            AddLayer(blend, layer, weight, xz, dx, dy, antiTiling);
        }
    }

    return ResolveBlend(blend);
}

// The map's macro tint and wetness, and world-noise variation (broad light and dark, smaller warm and cool
// patches), scaled by the layers' macro strength.
void ApplyMacro(inout TerrainSurface surface, float2 uv, float2 positionXZ)
{
    half4 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_LinearClamp, uv);
    half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LinearRepeat, positionXZ / _VariationSize).gb;
    half light = (noise.x - 0.5) * 2.0 * _Variation;
    half warmth = (noise.y - 0.5) * _Variation;
    half3 variation = 1.0 + half3(light + warmth, light, light - warmth);
    surface.albedo *= lerp(1.0, macro.rgb * 2.0 * variation, surface.macro);

    half wetness = macro.a;
    surface.albedo *= 1.0 - _WetDarkening * wetness;
    surface.smoothness = lerp(surface.smoothness, _WetSmoothness, wetness);
    surface.normalTS = normalize(lerp(surface.normalTS, half3(0.0, 0.0, 1.0), wetness * 0.5));
}

#endif
