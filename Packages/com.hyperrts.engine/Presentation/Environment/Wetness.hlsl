// Ground wetness after rain, from Wetness.cs, and the wet and puddle shading terrain and roads share.
#ifndef HYPERRTS_WETNESS_INCLUDED
#define HYPERRTS_WETNESS_INCLUDED

float _HyperRTSWetness; // 0 dry, 1 soaked

#define PUDDLE_SHORE 0.08     // collect range over which a puddle's edge fades in
#define PUDDLE_DARKENING 0.55 // standing water over soil

bool RainWet()
{
    return _HyperRTSWetness > 0.0;
}

// Wet ground: darker, glossier, its fine bumps filled in.
void Wet(inout half3 albedo, inout half smoothness, inout half3 normalTS, half wetness, half darkening, half gloss)
{
    albedo *= 1.0 - darkening * wetness;
    smoothness = lerp(smoothness, max(smoothness, gloss), wetness);
    normalTS = normalize(lerp(normalTS, half3(0.0, 0.0, 1.0), wetness * 0.5));
}

// Puddle cover where collect (how readily a spot gathers water, about 0..1) rises past the line wetness lowers.
half PuddleCover(half collect, half coverage)
{
    half level = 1.0 - _HyperRTSWetness * coverage;
    return smoothstep(level, level + PUDDLE_SHORE, collect);
}

// Standing water: dark, mirror-smooth and flat.
void Puddle(inout half3 albedo, inout half smoothness, inout half3 normalTS, half cover, half gloss)
{
    albedo *= 1.0 - PUDDLE_DARKENING * cover;
    smoothness = lerp(smoothness, gloss, cover);
    normalTS = normalize(lerp(normalTS, half3(0.0, 0.0, 1.0), cover));
}

#endif
