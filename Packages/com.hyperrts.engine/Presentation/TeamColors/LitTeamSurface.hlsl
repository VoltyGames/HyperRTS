// HyperRTS/LitTeam surface: URP Lit's InitializeStandardLitSurfaceData (LitInput.hlsl declares it beside Lit's own
// properties, so it can't be included) without detail maps, the specular workflow or transparency, and the team
// tint. Included by LitTeamInput.hlsl after its accessors.
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
    outSurfaceData.emission = SampleEmission(uv) * _LightsOn;
}

// Half the soft edge of the team strip band, in metres.
#define TEAM_STRIP_SOFTNESS 0.025

// Where team colour goes: the mask's R inside the band of object-space heights _TeamStrip.x..y.
half TeamMask(float2 uv0, float objectY)
{
    half painted = SAMPLE_TEXTURE2D(_TeamMask, sampler_TeamMask, uv0).r;
    half aboveBottom = smoothstep(_TeamStrip.x - TEAM_STRIP_SOFTNESS, _TeamStrip.x + TEAM_STRIP_SOFTNESS, objectY);
    half belowTop = 1.0 - smoothstep(_TeamStrip.y - TEAM_STRIP_SOFTNESS, _TeamStrip.y + TEAM_STRIP_SOFTNESS, objectY);
    return painted * aboveBottom * belowTop;
}

// Repaints masked albedo in the team colour, keeping its shading; _TeamColor.a 0 (neutral) leaves it untouched.
half3 ApplyTeamColor(half3 albedo, float2 uv0, float3 positionWS)
{
    // Uncoloured meshes (neutral, no player yet) skip the mask sample and object-space transform.
    if (_TeamColor.a == 0)
    {
        return albedo;
    }

    half amount = TeamMask(uv0, TransformWorldToObject(positionWS).y) * half(_TeamColor.a);
    half3 painted = half3(_TeamColor.rgb) * saturate(Luminance(albedo) * half(2.2));
    return lerp(albedo, painted, amount);
}

#endif
