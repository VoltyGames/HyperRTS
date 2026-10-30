// HyperRTS/Terrain remains: the rubble, ash, fuel and wet mud a match leaves (_HyperRTSRemains), over the layered
// ground. Rubble, ash and mud are tiled textures (albedo, normal, mask map with G occlusion and A smoothness).
#ifndef HYPERRTS_TERRAIN_REMAINS_INCLUDED
#define HYPERRTS_TERRAIN_REMAINS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayers.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wetness.hlsl"

#define REMAINS_NOISE_SIZE 3.0 // metres
#define REMAINS_EDGE 0.45      // how far the noise eats into thin cover
#define REMAINS_FRINGE 0.3
#define FUEL_ALBEDO half3(0.018, 0.016, 0.014)
#define FUEL_SMOOTHNESS 0.88
#define FUEL_SHEEN 0.08
#define MUD_DARKENING 0.35
#define MUD_GLOSS 0.6

TEXTURE2D(_RemainsRubbleAlbedo);
TEXTURE2D(_RemainsRubbleNormal);
TEXTURE2D(_RemainsRubbleMask);
TEXTURE2D(_RemainsAshAlbedo);
TEXTURE2D(_RemainsAshNormal);
TEXTURE2D(_RemainsAshMask);
TEXTURE2D(_RemainsMudAlbedo);
TEXTURE2D(_RemainsMudNormal);
TEXTURE2D(_RemainsMudMask);

// Explicit gradients: it runs inside the remains branches.
void BlendRemains(inout TerrainSurface surface, TerrainPixel pixel, half cover, float tile, half smoothness,
    TEXTURE2D_PARAM(albedoMap, albedoSampler), TEXTURE2D_PARAM(normalMap, normalSampler),
    TEXTURE2D_PARAM(maskMap, maskSampler))
{
    float2 st = pixel.xz / tile;
    float2 dx = pixel.dx / tile;
    float2 dy = pixel.dy / tile;
    half3 albedo = SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, st, dx, dy).rgb;
    half3 normal = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, st, dx, dy));
    half4 mask = SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, st, dx, dy);
    surface.albedo = lerp(surface.albedo, albedo, cover);
    surface.normalTS = normalize(lerp(surface.normalTS, normal, cover));
    surface.smoothness = lerp(surface.smoothness, mask.a * smoothness, cover);
    surface.occlusion *= lerp(1.0, mask.g, cover);
}

// A thin film's colours shift with the view angle and the slick's thickness, faintly and mostly at grazing angles.
void ApplyFuel(inout TerrainSurface surface, TerrainPixel pixel, half cover, half noise)
{
    half facing = saturate(dot(GetWorldSpaceNormalizeViewDir(pixel.positionWS), pixel.frame[2]));
    half grazing = Sq(1.0 - facing);
    half3 film = 0.5 + 0.5 * cos(TWO_PI * (facing * 2.5 + noise * 1.5 + half3(0.0, 0.33, 0.67)));
    surface.albedo = lerp(surface.albedo, FUEL_ALBEDO + film * (FUEL_SHEEN * grazing), cover);
    surface.smoothness = lerp(surface.smoothness, FUEL_SMOOTHNESS, cover);
    surface.normalTS = normalize(lerp(surface.normalTS, half3(0.0, 0.0, 1.0), cover * 0.8));
}

// Returns the wet mud's cover, where puddles gather too.
half ApplyRemains(inout TerrainSurface surface, TerrainPixel pixel)
{
    UNITY_BRANCH
    if (!InteractionMapsOn())
    {
        return 0.0;
    }

    half4 remains = SAMPLE_TEXTURE2D_LOD(_HyperRTSRemains, sampler_LinearClamp, InteractionUV(pixel.xz), 0);
    UNITY_BRANCH
    if (!any(remains > 0.0))
    {
        return 0.0;
    }

    // Noise eats into thin cover, so a disc's fringe breaks up instead of reading as a circle.
    half noise = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, pixel.xz / REMAINS_NOISE_SIZE,
        pixel.dx / REMAINS_NOISE_SIZE, pixel.dy / REMAINS_NOISE_SIZE).b;
    half edge = noise * REMAINS_EDGE;
    half4 cover = smoothstep(edge, edge + REMAINS_FRINGE, remains);

    UNITY_BRANCH
    if (cover.a > 0.0)
    {
        BlendRemains(surface, pixel, cover.a, _RemainsMudTile, _RemainsMudSmoothness,
            TEXTURE2D_ARGS(_RemainsMudAlbedo, sampler_LinearRepeat), TEXTURE2D_ARGS(_RemainsMudNormal, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_RemainsMudMask, sampler_LinearRepeat));
        Wet(surface.albedo, surface.smoothness, surface.normalTS, cover.a, MUD_DARKENING, MUD_GLOSS);
    }

    UNITY_BRANCH
    if (cover.g > 0.0)
    {
        BlendRemains(surface, pixel, cover.g, _RemainsAshTile, _RemainsAshSmoothness,
            TEXTURE2D_ARGS(_RemainsAshAlbedo, sampler_LinearRepeat), TEXTURE2D_ARGS(_RemainsAshNormal, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_RemainsAshMask, sampler_LinearRepeat));
    }

    UNITY_BRANCH
    if (cover.r > 0.0)
    {
        BlendRemains(surface, pixel, cover.r, _RemainsRubbleTile, _RemainsRubbleSmoothness,
            TEXTURE2D_ARGS(_RemainsRubbleAlbedo, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_RemainsRubbleNormal, sampler_LinearRepeat), TEXTURE2D_ARGS(_RemainsRubbleMask, sampler_LinearRepeat));
    }

    UNITY_BRANCH
    if (cover.b > 0.0)
    {
        ApplyFuel(surface, pixel, cover.b, noise);
    }
    return cover.a;
}

#endif
