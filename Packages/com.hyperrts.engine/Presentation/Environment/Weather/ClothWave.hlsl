// The ripple cloth shows in the scene's wind, shared by placed flags and cloth modelled into a mesh.
#ifndef HYPERRTS_CLOTH_WAVE_INCLUDED
#define HYPERRTS_CLOTH_WAVE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/WindGlobals.hlsl"

struct ClothWave
{
    float offset;
    float slope;
};

// Runs faster and wider in wind, scaled by weight from the hoist out; each origin starts its own phase, so a row of
// cloths never waves in step. The slope is per unit of along, which k counts waves over.
ClothWave WaveCloth(float along, float k, float speed, float amplitude, float weight, float3 origin, float time)
{
    float strength = saturate(_HyperRTSWindStrength);
    float phase = along * k - time * speed * (0.6 + strength) + dot(origin.xz, float2(0.37, 0.23));
    float size = amplitude * (0.5 + strength) * weight;
    ClothWave wave;
    wave.offset = size * sin(phase);
    wave.slope = size * k * cos(phase);
    return wave;
}

#endif
