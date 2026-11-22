// HyperRTS/Terrain wetness: the ground tint map's damp hollows, raised by the weather's wetness (Wetness.hlsl).
#ifndef HYPERRTS_TERRAIN_WETNESS_INCLUDED
#define HYPERRTS_TERRAIN_WETNESS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"

// How readily water stands here: broad world noise, damp hollows and ruts place whole pools; the fine noise and low
// texels only ragged their edges, or every texel dip would hold a fleck. The broad noise mixes two unrelated scales
// so the pools never line up on the tile. Explicit gradients, as it runs in a branch.
half PuddleCollect(TerrainSurface surface, half dampness, half rut, float2 st, float2 dx, float2 dy)
{
    half near = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, st, dx, dy).g;
    half wide = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, st * 0.41 + 0.37, dx * 0.41, dy * 0.41).g;
    half fine = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_LinearRepeat, st * 2.7, dx * 2.7, dy * 2.7).b;
    half broad = (near + wide) * 0.5;
    half edge = (fine - 0.5) * 0.3 + (0.5 - surface.height) * 0.3;
    return (broad - 0.5) * 2.0 + dampness * 0.6 + rut * 1.2 + edge;
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

    // Grass sheds rain, so it darkens and pools less.
    half shed = lerp(1.0, _GrassWetness, surface.grass);
    // Rain has its own, less glossy wetting: from the camera's pitch a glossy field mirrors the sky and turns grey.
    Wet(surface.albedo, surface.smoothness, surface.normalTS, max(_HyperRTSWetness * shed - dampness, 0.0),
        _RainDarkening, _RainSmoothness);
    half collect = PuddleCollect(surface, dampness, rut, st, dx, dy) * shed;
    half puddles = RainPuddles(surface.albedo, surface.smoothness, surface.normalTS, collect, _PuddleCoverage,
        _PuddleSmoothness);
    surface.occlusion = lerp(surface.occlusion, 1.0, puddles * 0.5);
}

#endif
