// Fog of war for Particles.shader and ParticlesUnlit.shader, whose URP forward pass already applies URP's fog. Needs
// a URP particle forward pass first.
#ifndef HYPERRTS_PARTICLES_FOG_INCLUDED
#define HYPERRTS_PARTICLES_FOG_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

static const half AdditiveBlend = 2.0h;

bool IsAdditiveBlend()
{
    return _Blend == AdditiveBlend;
}

// Fog hides additive light and pulls blended colour toward the fog colour.
half4 FogParticle(half4 color, float3 positionWS)
{
    // The full-screen fog pass already covers opaque particles.
    if (!IsSurfaceTypeTransparent())
    {
        return color;
    }

    half4 fog = FogOfWarAt(positionWS);
    if (IsAdditiveBlend())
    {
        color.rgb *= 1.0h - fog.a;
        return color;
    }

    fog.rgb *= GetPreExposureMultiplier();
    if (UseAlphaPremultiply())
    {
        fog.rgb *= color.a;
    }

    color.rgb = lerp(color.rgb, fog.rgb, fog.a);
    return color;
}

#endif
