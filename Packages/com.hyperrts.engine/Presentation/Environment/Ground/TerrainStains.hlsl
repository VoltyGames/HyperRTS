// HyperRTS/Terrain stains: the rubble, ash, fuel and wet mud a match leaves (_HyperRTSStains), over the layered
// ground. Rubble, ash and mud are tiled textures (albedo, normal, mask map with G occlusion and A smoothness).
#ifndef HYPERRTS_TERRAIN_STAINS_INCLUDED
#define HYPERRTS_TERRAIN_STAINS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainLayers.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/Trample.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"

#define STAIN_NOISE_SIZE 3.0 // metres
#define STAIN_EDGE 0.45      // how far the noise eats into thin cover
#define STAIN_FRINGE 0.3
#define FUEL_ALBEDO half3(0.018, 0.016, 0.014)
#define FUEL_SMOOTHNESS 0.88
#define FUEL_SHEEN 0.08
#define MUD_DARKENING 0.35
#define MUD_GLOSS 0.6
#define CHAR_START 0.6                    // ash from which the ground turns to char: field fires, not craters
#define CHAR_SMUDGE_SIZE float2(4.0, 9.0) // metres across and along the broad grey ash smudges
#define CHAR_DETAIL_SIZE float2(1.7, 3.1) // a finer, unaligned scale that breaks the smudges' shapes
#define CHAR_SMUDGE_EDGE 0.12             // soft edges
#define CHAR_ASH_STRENGTH 0.75            // the grey never fully covers the char
#define CHAR_PATCH_SIZE 11.0              // metres over which the char shades lighter or darker
#define CHAR_FLATTEN 0.85                 // share of the ground's bumps the char smooths away
#define ASH_GREY half3(0.3, 0.29, 0.28)
#define CHAR_SMOOTHNESS 0.08

TEXTURE2D(_StainRubbleAlbedo);
TEXTURE2D(_StainRubbleNormal);
TEXTURE2D(_StainRubbleMask);
TEXTURE2D(_StainAshAlbedo);
TEXTURE2D(_StainAshNormal);
TEXTURE2D(_StainAshMask);
TEXTURE2D(_StainMudAlbedo);
TEXTURE2D(_StainMudNormal);
TEXTURE2D(_StainMudMask);

// Explicit gradients: it runs inside the stain branches.
void BlendStain(inout TerrainSurface surface, TerrainPixel pixel, half cover, float tile, half smoothness,
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

half CharNoise(TerrainPixel pixel, float2 size)
{
    return SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, pixel.xz / size, pixel.dx / size, pixel.dy / size).g;
}

// Heavy ash is a burnt field: near-black char with a few soft grey ash smudges. Untextured, so no tiling shows.
void ApplyChar(inout TerrainSurface surface, TerrainPixel pixel, half ash, half cover)
{
    // Two scales blended and thresholded smoothly, so the smudges are sparse, soft and never repeat a shape.
    half smudge = lerp(CharNoise(pixel, CHAR_SMUDGE_SIZE), CharNoise(pixel, CHAR_DETAIL_SIZE), 0.35);
    half start = 1.0 - _StainCharStreaks;
    half grey = smoothstep(start, start + CHAR_SMUDGE_EDGE, smudge) * CHAR_ASH_STRENGTH;
    half patch = CharNoise(pixel, CHAR_PATCH_SIZE);
    half3 burnt = lerp(_StainCharColor.rgb * lerp(0.8, 1.2, patch), ASH_GREY, grey);
    half charred = smoothstep(CHAR_START, 1.0, ash) * cover;
    surface.albedo = lerp(surface.albedo, burnt, charred);
    surface.smoothness = lerp(surface.smoothness, CHAR_SMOOTHNESS, charred);
    surface.normalTS = normalize(lerp(surface.normalTS, half3(0.0, 0.0, 1.0), charred * CHAR_FLATTEN));
}

// Returns the wet mud's cover, where puddles gather too.
half ApplyStains(inout TerrainSurface surface, TerrainPixel pixel)
{
    UNITY_BRANCH
    if (!TrampleMapsOn())
    {
        return 0.0;
    }

    half4 stains = SAMPLE_TEXTURE2D_LOD(_HyperRTSStains, sampler_LinearClamp, TrampleUV(pixel.xz), 0);
    UNITY_BRANCH
    if (!any(stains > 0.0))
    {
        return 0.0;
    }

    // Noise eats into thin cover, so a disc's fringe breaks up instead of reading as a circle.
    half noise = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, pixel.xz / STAIN_NOISE_SIZE,
        pixel.dx / STAIN_NOISE_SIZE, pixel.dy / STAIN_NOISE_SIZE).b;
    half edge = noise * STAIN_EDGE;
    half4 cover = smoothstep(edge, edge + STAIN_FRINGE, stains);

    UNITY_BRANCH
    if (cover.a > 0.0)
    {
        BlendStain(surface, pixel, cover.a, _StainMudTile, _StainMudSmoothness,
            TEXTURE2D_ARGS(_StainMudAlbedo, sampler_LinearRepeat), TEXTURE2D_ARGS(_StainMudNormal, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_StainMudMask, sampler_LinearRepeat));
        Wet(surface.albedo, surface.smoothness, surface.normalTS, cover.a, MUD_DARKENING, MUD_GLOSS);
    }

    UNITY_BRANCH
    if (cover.g > 0.0)
    {
        BlendStain(surface, pixel, cover.g, _StainAshTile, _StainAshSmoothness,
            TEXTURE2D_ARGS(_StainAshAlbedo, sampler_LinearRepeat), TEXTURE2D_ARGS(_StainAshNormal, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_StainAshMask, sampler_LinearRepeat));
        ApplyChar(surface, pixel, stains.g, cover.g);
    }

    UNITY_BRANCH
    if (cover.r > 0.0)
    {
        BlendStain(surface, pixel, cover.r, _StainRubbleTile, _StainRubbleSmoothness,
            TEXTURE2D_ARGS(_StainRubbleAlbedo, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_StainRubbleNormal, sampler_LinearRepeat), TEXTURE2D_ARGS(_StainRubbleMask, sampler_LinearRepeat));
    }

    UNITY_BRANCH
    if (cover.b > 0.0)
    {
        ApplyFuel(surface, pixel, cover.b, noise);
    }
    return cover.a;
}

#endif
