// HyperRTS/Terrain layer blending: an online softmax over log(weight) + height / depth, so the tallest texels win
// where layers mix without knowing every layer's height up front.
#ifndef HYPERRTS_TERRAIN_LAYERS_INCLUDED
#define HYPERRTS_TERRAIN_LAYERS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayerParams.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainPixel.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainTriplanar.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainParallax.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"

#define TERRAIN_MAX_LAYERS 16
#define TERRAIN_MIN_WEIGHT 0.003
// Layers with less of the blend so far skip their normal and mask samples.
#define TERRAIN_MIN_SHARE 0.02

struct TerrainBlend
{
    float maxScore;
    float total;
    float3 albedo;
    float3 normal;
    float smoothness;
    float occlusion;
    float height;
    float tintStrength;
    float grass;
};

struct TerrainSurface
{
    half3 albedo;
    half3 normalTS;    // terrain tangent space: x east, y north, z along the terrain normal
    half smoothness;
    half occlusion;
    half height;
    half tintStrength;
    half grass;
};

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
    blend.tintStrength *= keep;
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
    TerrainSides sides = GetSides(pixel, params, layer);
    half4 albedo = WithSides(LayerAlbedo(hex, st, dx, dy, pixel.far),
        TEXTURE2D_ARRAY_ARGS(_AlbedoArray, sampler_AlbedoArray), sides);

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
    blend.tintStrength += share * params.tintStrength;
    blend.grass += share * params.grass;

    half3 normal = half3(0.0, 0.0, 1.0);
    half2 mask = half2(1.0, 0.0);
    if (share > blend.total * TERRAIN_MIN_SHARE)
    {
        normal = NormalWithSides(LayerNormal(hex, params), sides, pixel, params.normalStrength);
        mask = WithSides(SampleHex(TEXTURE2D_ARRAY_ARGS(_MaskArray, sampler_AlbedoArray), hex),
            TEXTURE2D_ARRAY_ARGS(_MaskArray, sampler_AlbedoArray), sides).rg;
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
    surface.tintStrength = blend.tintStrength * scale;
    surface.grass = blend.grass * scale;
    return surface;
}

// The layer with the most painted weight, which parallax marches.
uint DominantLayer(half4 control0, half4 control1, half4 control2, half4 control3, uint count)
{
    uint dominant = 0;
    half most = 0.0;
    [loop] for (uint layer = 0; layer < count; layer++)
    {
        half weight = LayerWeight(control0, control1, control2, control3, layer);
        dominant = weight > most ? layer : dominant;
        most = max(most, weight);
    }

    return dominant;
}

TerrainSurface BlendLayers(float2 uv, inout TerrainPixel pixel)
{
    float2 controlUV = ControlUV(uv);
    half4 control0 = SAMPLE_TEXTURE2D(_Control0, sampler_LinearClamp, controlUV);
    half4 control1 = SAMPLE_TEXTURE2D(_Control1, sampler_LinearClamp, controlUV);
    half4 control2 = SAMPLE_TEXTURE2D(_Control2, sampler_LinearClamp, controlUV);
    half4 control3 = SAMPLE_TEXTURE2D(_Control3, sampler_LinearClamp, controlUV);

    uint count = min((uint)_LayerCount, TERRAIN_MAX_LAYERS);
    #ifdef TERRAIN_PARALLAX
        pixel.xz += ParallaxOffset(pixel, DominantLayer(control0, control1, control2, control3, count));
    #endif

    TerrainBlend blend = (TerrainBlend)0;
    blend.maxScore = -1e8;
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

// The map's ground tint and world-noise variation (broad light and dark, smaller warm and cool patches), scaled by the
// layers' tint strength. Returns the tint's dampness for TerrainWetness.hlsl.
half ApplyGroundTint(inout TerrainSurface surface, float2 uv, float2 positionXZ)
{
    half4 groundTint = SAMPLE_TEXTURE2D(_GroundTintMap, sampler_LinearClamp, uv);
    half2 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LinearRepeat, positionXZ / _VariationSize).gb;
    half light = (noise.x - 0.5) * 2.0 * _Variation;
    half warmth = (noise.y - 0.5) * _Variation;
    half3 variation = 1.0 + half3(light + warmth, light, light - warmth);
    surface.albedo *= lerp(1.0, groundTint.rgb * 2.0 * variation, surface.tintStrength);

    // The grass cards' dry and green patches, so grass and the ground under it change together.
    [branch] if (surface.grass * _GrassPatches != 0.0)
    {
        half patch = (half)(DryPatch(positionXZ) * 2.0 - 1.0) * _GrassPatches;
        surface.albedo *= lerp(1.0, 1.0 + patch * half3(1.0, 0.55, 0.15), surface.grass);
    }

    return groundTint.a;
}

#endif
