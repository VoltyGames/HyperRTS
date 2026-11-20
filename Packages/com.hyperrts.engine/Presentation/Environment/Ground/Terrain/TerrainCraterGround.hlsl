// HyperRTS/Terrain crater ground: the earth a blast dug up, over the bowls it dented in the tracks map. Projected from three sides, so a steep bowl wall keeps its texel size. The includer declares the
// _StainCrater* textures' tile and smoothness with the other _Stain* fields.
#ifndef HYPERRTS_TERRAIN_CRATER_GROUND_INCLUDED
#define HYPERRTS_TERRAIN_CRATER_GROUND_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainStains.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Triplanar.hlsl"

#define CRATER_GROUND_START 0.03 // metres dented from which dug earth shows
#define CRATER_GROUND_FULL 0.4   // and from which it covers the ground
#define CRATER_GROUND_COVER 0.9  // a little of the char and ash under it still shows through
#define CRATER_GROUND_CHAR 0.45  // how much darker full scorch leaves the earth thrown over it

TEXTURE2D(_StainCraterAlbedo);
TEXTURE2D(_StainCraterNormal);
TEXTURE2D(_StainCraterMask);

// One side projection (SideProjection's side 0 or 1). Returns its normal in world space.
StainSample SampleCraterSide(TerrainPixel pixel, uint side, half3 normalWS)
{
    half3 axisSigns = AxisSigns(normalWS);
    float2 uv = SideProjection(pixel.positionWS, side, axisSigns) / _StainCraterTile;
    float2 uvDx = SideProjection(pixel.dPdx, side, axisSigns) / _StainCraterTile;
    float2 uvDy = SideProjection(pixel.dPdy, side, axisSigns) / _StainCraterTile;

    StainSample projected;
    projected.albedo = SAMPLE_TEXTURE2D_GRAD(_StainCraterAlbedo, sampler_LinearRepeat, uv, uvDx, uvDy).rgb;
    projected.mask = SAMPLE_TEXTURE2D_GRAD(_StainCraterMask, sampler_LinearRepeat, uv, uvDx, uvDy);
    half3 t = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_StainCraterNormal, sampler_LinearRepeat, uv, uvDx, uvDy));
    projected.normal = SideNormal(t, side, axisSigns, normalWS);
    return projected;
}

// The earth from above, hex tiled, with the two sides mixed in by how steep the ground is. Normal in tangent space.
StainSample SampleCraterGround(TerrainPixel pixel)
{
    half3 normalWS = pixel.frame[2];
    half3 weights = TriplanarWeights(normalWS);
    StainSample ground = (StainSample)0;
    [branch] if (weights.y > HEX_MIN_WEIGHT)
    {
        StainSample top = SampleHexStain(pixel, _StainCraterTile, TEXTURE2D_ARGS(_StainCraterAlbedo, sampler_LinearRepeat),
            TEXTURE2D_ARGS(_StainCraterNormal, sampler_LinearRepeat), TEXTURE2D_ARGS(_StainCraterMask, sampler_LinearRepeat));
        ground.albedo = weights.y * top.albedo;
        ground.mask = weights.y * top.mask;
        ground.normal = weights.y * top.normal;
    }

    half2 sideWeights = weights.xz;
    [unroll] for (uint i = 0; i < 2; i++)
    {
        [branch] if (sideWeights[i] > HEX_MIN_WEIGHT)
        {
            StainSample side = SampleCraterSide(pixel, i, normalWS);
            ground.albedo += sideWeights[i] * side.albedo;
            ground.mask += sideWeights[i] * side.mask;
            ground.normal += sideWeights[i] * mul(pixel.frame, side.normal);
        }
    }

    ground.normal = normalize(ground.normal);
    return ground;
}

// Dug earth over the char and ash in the bowls scars dented, darker where the blast scorched them; the thrown-earth
// lips keep the burn's char and rays. Takes the metres sunk and the scorch from the tracks map the caller read.
void ApplyCraterGround(inout TerrainSurface surface, TerrainPixel pixel, float sunk, half scorch)
{
    UNITY_BRANCH
    if (sunk <= 0.0)
    {
        return;
    }

    half noise = CharNoise(pixel, STAIN_NOISE_SIZE);
    half cover = smoothstep(CRATER_GROUND_START, CRATER_GROUND_FULL, sunk * (0.6h + 0.8h * noise));
    UNITY_BRANCH
    if (cover <= 0.0)
    {
        return;
    }

    StainSample ground = SampleCraterGround(pixel);
    ground.albedo *= 1.0h - CRATER_GROUND_CHAR * scorch;
    MixStain(surface, cover * CRATER_GROUND_COVER, _StainCraterSmoothness, ground.albedo, ground.normal, ground.mask);
}

#endif
