// Surface setup: URP Lit's InitializeStandardLitSurfaceData without detail maps or specular workflow, plus the team
// tint. Included by LitTeamInput.hlsl.
#ifndef HYPERRTS_LIT_TEAM_SURFACE_INCLUDED
#define HYPERRTS_LIT_TEAM_SURFACE_INCLUDED

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleBaseMap(uv);
    outSurfaceData = (SurfaceData)0;
    outSurfaceData.alpha = AlphaDiscard(albedoAlpha.a * GetBaseColor().a, GetCutoff());
    outSurfaceData.albedo = albedoAlpha.rgb * GetBaseColor().rgb;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv);
    outSurfaceData.occlusion = SampleOcclusion(uv);
#if !defined(_INTERIOR)
    outSurfaceData.emission = SampleEmission(uv) * _LightLevel;
#endif
}

#define TEAM_STRIP_SOFTNESS 0.025

// Where team colour goes: the mask's R inside the band of object-space heights _TeamStrip.x..y.
half TeamMask(float2 uv0, float objectY)
{
    half painted = SAMPLE_TEXTURE2D(_TeamMask, sampler_TeamMask, uv0).r;
    half aboveBottom = smoothstep(_TeamStrip.x - TEAM_STRIP_SOFTNESS, _TeamStrip.x + TEAM_STRIP_SOFTNESS, objectY);
    half belowTop = 1.0 - smoothstep(_TeamStrip.y - TEAM_STRIP_SOFTNESS, _TeamStrip.y + TEAM_STRIP_SOFTNESS, objectY);
    return painted * aboveBottom * belowTop;
}

// Paint reads as a worn coat, not a flat fill: a little duller and darker than the pure colour, with some of the
// surface under it showing through.
#define TEAM_PAINT_CHROMA 0.92
#define TEAM_PAINT_VALUE 0.9
#define TEAM_PAINT_COVER 0.9

// Repaints albedo under teamMask in the team colour, keeping its shading; _TeamColor.a 0 (neutral) leaves it untouched.
half3 ApplyTeamColor(half3 albedo, half teamMask)
{
    half amount = teamMask * half(_TeamColor.a);
    half3 team = half3(_TeamColor.rgb);
    half3 coat = lerp(Luminance(team).xxx, team, half(TEAM_PAINT_CHROMA)) * half(TEAM_PAINT_VALUE);
    half3 painted = coat * saturate(Luminance(albedo) * half(2.2));
    return lerp(albedo, painted, amount * half(TEAM_PAINT_COVER));
}

#endif
