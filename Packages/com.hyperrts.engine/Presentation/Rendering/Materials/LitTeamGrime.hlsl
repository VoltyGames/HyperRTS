// HyperRTS/LitTeam grime: dust, mud, wet and soot on a ground vehicle's albedo, smoothness and metallic. The levels are
// _Grime (dust, mud, wet, soot) and the height band is _GrimeBand; both are per instance, written by GrimeSystem.
#ifndef HYPERRTS_LIT_TEAM_GRIME_INCLUDED
#define HYPERRTS_LIT_TEAM_GRIME_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

float4 _HyperRTSDustColor;
float4 _HyperRTSMudColor;

void ApplyGrime(float3 positionWS, inout SurfaceData surface)
{
    // Clean vehicles skip the object-space transform and every noise sample.
    if (!any(_Grime))
    {
        return;
    }

    float3 positionOS = TransformWorldToObject(positionWS);
    float height = positionOS.y - _GrimeBand.x;
    float mudTop = _GrimeBand.y;
    float dustTop = _GrimeBand.z;
    float coarse = ValueNoise(positionOS * 7.0);
    // Without mud or soot their noise alone stays below the threshold, so it is not sampled.
    float fine = 0.0;
    if (_Grime.y > 0.0)
    {
        fine = ValueNoise(positionOS * 29.0);
    }
    half3 albedo = surface.albedo;
    half smoothness = surface.smoothness;
    half metallic = surface.metallic;

    // Thinner with height, and broken up by the noise.
    float dustReach = 1.0 - smoothstep(0.0, dustTop, height);
    float dustCut = 1.0 - smoothstep(dustTop * 0.8, dustTop, height);
    half dust = saturate((_Grime.x * (0.4 + dustReach * 0.9) + (coarse - 0.5) * 0.5 - 0.25) * 4.0) * dustCut;
    albedo = lerp(albedo, _HyperRTSDustColor.rgb, dust * 0.85);
    smoothness = lerp(smoothness, smoothness * 0.35, dust);

    float mudReach = 1.0 - smoothstep(0.5 * mudTop, mudTop, height);
    float mudCut = 1.0 - smoothstep(0.75 * mudTop, mudTop, height);
    half mud = saturate((_Grime.y * (0.5 + mudReach) + (fine * 0.6 + coarse * 0.4 - 0.5) * 0.7 - 0.36) * 5.0) * mudCut;
    albedo = lerp(albedo, _HyperRTSMudColor.rgb, mud * 0.95);
    smoothness = lerp(smoothness, 0.3, mud);

    half wet = _Grime.z * (1.0 - smoothstep(0.0, mudTop * 1.5, height));
    albedo *= lerp(1.0, 0.6, wet);
    smoothness = saturate(smoothness + wet * 0.25);

    half soot = 0.0;
    if (_Grime.w > 0.0)
    {
        soot = saturate((_Grime.w * 1.1 + (ValueNoise(positionOS * 3.0) - 0.5) * 0.8 - 0.42) * 3.0);
    }
    albedo *= lerp(1.0, 0.25, soot * 0.85);
    smoothness *= lerp(1.0, 0.4, soot);

    surface.albedo = albedo;
    surface.smoothness = smoothness;
    surface.metallic = metallic * (1.0 - max(dust, mud));
}

#endif
