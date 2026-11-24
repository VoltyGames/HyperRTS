// Cloth waving for flags, pennants and banners modelled into a mesh: a ripple running out from the hoist edge.
#ifndef HYPERRTS_LIT_TEAM_CLOTH_INCLUDED
#define HYPERRTS_LIT_TEAM_CLOTH_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/WindGlobals.hlsl"

// Nothing moves at or behind the hoist, so a pole or rod on the same material stays put; the ripple grows to the fly end
// and runs faster and wider in wind. Each mesh starts its own phase, so a row of flags never waves in step.
void ApplyCloth(float time, inout float3 positionOS, inout float3 normalOS)
{
    float3 along = _ClothAlong.xyz;
    float length2 = max(dot(along, along), 1e-6);
    float reach = saturate(dot(positionOS - _ClothHoist.xyz, along) / length2);
    float strength = saturate(_HyperRTSWindStrength);
    float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
    float k = TWO_PI * _ClothWaves;
    float phase = reach * k - time * _ClothSpeed * (0.6 + strength) + dot(origin.xz, float2(0.37, 0.23));
    float amplitude = _ClothAmplitude * (0.5 + strength) * reach;
    float3 side = normalize(_ClothSide.xyz);
    positionOS += side * (amplitude * sin(phase));
    // The ripple's slope per metre along the cloth tips the normal toward the direction it runs.
    float slope = amplitude * k * cos(phase) / sqrt(length2);
    normalOS = normalize(normalOS - dot(normalOS, side) * slope * along / sqrt(length2));
}

#endif
