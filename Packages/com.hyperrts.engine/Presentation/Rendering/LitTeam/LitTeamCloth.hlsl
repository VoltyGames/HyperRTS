// Cloth waving for flags, pennants and banners modelled into a mesh: a ripple running out from the hoist edge.
#ifndef HYPERRTS_LIT_TEAM_CLOTH_INCLUDED
#define HYPERRTS_LIT_TEAM_CLOTH_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/ClothWave.hlsl"

// Nothing moves at or behind the hoist, so a pole or rod on the same material stays put; the ripple grows to the fly end.
void ApplyCloth(float time, inout float3 positionOS, inout float3 normalOS)
{
    float3 along = _ClothAlong.xyz;
    float length2 = max(dot(along, along), 1e-6);
    float reach = saturate(dot(positionOS - _ClothHoist.xyz, along) / length2);
    float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
    ClothWave wave = WaveCloth(reach, TWO_PI * _ClothWaves, _ClothSpeed, _ClothAmplitude, reach, origin, time);
    float3 side = normalize(_ClothSide.xyz);
    positionOS += side * wave.offset;
    // The ripple's slope per metre along the cloth tips the normal toward the direction it runs.
    float slope = wave.slope / sqrt(length2);
    normalOS = normalize(normalOS - dot(normalOS, side) * slope * along / sqrt(length2));
}

#endif
