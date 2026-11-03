// The horizon's fade into the sky, shared by HyperRTS/HorizonGround and HyperRTS/HorizonBelt: over the ring's outer
// share the lit colour gives way to the sky's horizon colour at the pixel's bearing. HorizonSky sets every property:
//   _SkyTex, _SkyTint, _SkyExposure, _SkyBlur  the skybox's _Tex, _Tint, _Exposure, _HorizonBlur
//   _SkyTurn                                   cos and sin of the skybox's _Rotation
//   _SkyFade                                   metres past the terrain's edge where the fade starts, ends
//   _TerrainRect                               the terrain's x min, z min, x max, z max
// The including shader declares all but _SkyTex in its UnityPerMaterial buffer, _SkyTex_HDR too, before including this.
#ifndef HYPERRTS_HORIZON_SKY_FADE_INCLUDED
#define HYPERRTS_HORIZON_SKY_FADE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/SkyHorizon.hlsl"

TEXTURECUBE(_SkyTex);
SAMPLER(sampler_SkyTex);

half3 HorizonSkyFade(half3 color, float3 positionWS)
{
    float2 nearest = clamp(positionWS.xz, _TerrainRect.xy, _TerrainRect.zw);
    half fade = smoothstep(_SkyFade.x, _SkyFade.y, length(positionWS.xz - nearest));
    half3 faded = color;
    [branch] if (fade > 0.0)
    {
        float2 bearing = SkyBearing(normalize(positionWS.xz - _WorldSpaceCameraPos.xz), _SkyTurn.xy);
        half3 horizon = SkyHorizonColor(TEXTURECUBE_ARGS(_SkyTex, sampler_SkyTex), _SkyTex_HDR, _SkyTint.rgb,
            _SkyExposure, bearing, _SkyBlur);
        faded = lerp(color, horizon, fade);
    }

    return faded;
}

#endif
