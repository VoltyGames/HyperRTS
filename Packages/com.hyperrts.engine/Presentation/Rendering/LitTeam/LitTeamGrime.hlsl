// Grime (dust, mud, wet and soot that GrimeSystem sets per ground vehicle) and rain wetness on every unit and
// building.
#ifndef HYPERRTS_LIT_TEAM_GRIME_INCLUDED
#define HYPERRTS_LIT_TEAM_GRIME_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Hash.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"

// Paint darkens less than soil when wet, and wet metal not at all.
#define RAIN_DARKENING 0.3
#define RAIN_GLOSS 0.7

// Dust is a film over the paint, thickest by the wheels: a hull at full dust still shows its camouflage.
#define DUST_FILM_FOOT 0.55
#define DUST_FILM_TOP 0.2

float4 _HyperRTSDustColor;
float4 _HyperRTSMudColor;

// Team paint stays clean of dust, as it does of weathering, so a dusty hull still shows its owner.
void ApplyGrime(float3 positionWS, half teamMask, inout SurfaceData surface)
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
    dust *= 1.0 - teamMask;
    albedo = lerp(albedo, _HyperRTSDustColor.rgb, dust * lerp(DUST_FILM_TOP, DUST_FILM_FOOT, dustReach));
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

// After rain every unit and building is wet where the sky reaches it, as the ground is: tops fully, sides half.
void ApplyRain(half3 normalWS, inout SurfaceData surface)
{
    if (!RainWet())
    {
        return;
    }

    half wetness = _HyperRTSWetness * saturate(normalWS.y * 0.5 + 0.5);
    Wet(surface.albedo, surface.smoothness, surface.normalTS, wetness, RAIN_DARKENING * (1.0 - surface.metallic),
        RAIN_GLOSS);
}

#endif
