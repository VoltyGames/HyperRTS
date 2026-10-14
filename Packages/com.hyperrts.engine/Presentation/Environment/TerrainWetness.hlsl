// HyperRTS/Terrain wetness: the macro map's damp hollows, raised everywhere by the weather's wetness (Wetness.hlsl),
// which also pools puddles in low texels, damp hollows and ruts.
#ifndef HYPERRTS_TERRAIN_WETNESS_INCLUDED
#define HYPERRTS_TERRAIN_WETNESS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Wetness.hlsl"

// 1 on green albedo, 0 on bare soil: grass sheds rain, so it darkens and pools less.
half Greenness(half3 albedo)
{
    return saturate((albedo.g - max(albedo.r, albedo.b)) * 10.0);
}

// How readily water stands here: low texels, damp hollows and ruts, broken up by two octaves of world noise.
// Explicit gradients: it runs inside the wetness branch.
half PuddleCollect(TerrainSurface surface, half dampness, half rut, float2 st, float2 dx, float2 dy)
{
    half broad = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, st, dx, dy).g;
    half fine = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, st * 2.7, dx * 2.7, dy * 2.7).b;
    half low = saturate((0.55 - surface.height) * 2.5);
    half noise = (broad - 0.5) * 1.2 + (fine - 0.5) * 0.5;
    return low * 0.5 + dampness * 0.5 + rut * 1.2 + noise;
}

void ApplyWetness(inout TerrainSurface surface, half dampness, half rut, float2 positionXZ)
{
    Wet(surface.albedo, surface.smoothness, surface.normalTS, dampness, _WetDarkening, _WetSmoothness);
    float2 st = positionXZ / _PuddleNoiseSize;
    float2 dx = ddx(st);
    float2 dy = ddy(st);

    UNITY_BRANCH
    if (!RainWet())
    {
        return;
    }

    half grass = Greenness(surface.albedo);
    half rain = _HyperRTSWetness * lerp(1.0, _GrassWetness, grass);
    // Rain has its own, less glossy wetting: from the camera's pitch a glossy field mirrors the sky and turns grey.
    Wet(surface.albedo, surface.smoothness, surface.normalTS, max(rain - dampness, 0.0), _RainDarkening, _RainSmoothness);
    half collect = PuddleCollect(surface, dampness, rut, st, dx, dy) * (1.0 - 0.6 * grass);
    half cover = PuddleCover(collect, _PuddleCoverage);
    Puddle(surface.albedo, surface.smoothness, surface.normalTS, cover, _PuddleSmoothness);
    surface.occlusion = lerp(surface.occlusion, 1.0, cover * 0.5);
}

#endif
