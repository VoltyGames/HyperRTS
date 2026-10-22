// Height fog and fog of war for Particles.shader and ParticlesUnlit.shader. Needs a URP particle forward pass first.
#ifndef HYPERRTS_PARTICLES_FOG_INCLUDED
#define HYPERRTS_PARTICLES_FOG_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneFog.hlsl"

static const half AdditiveBlend = 2.0h;

bool IsAdditiveBlend()
{
    return _Blend == AdditiveBlend;
}

// Fog hides additive light and pulls blended colour toward the fog colour.
half3 FogParticleLayer(half3 color, half alpha, half4 fog)
{
    if (IsAdditiveBlend())
    {
        return color * (1.0h - fog.a);
    }

    fog.rgb *= GetPreExposureMultiplier();
    if (UseAlphaPremultiply())
    {
        fog.rgb *= alpha;
    }

    return ApplyFog(color, fog);
}

half4 FogParticle(half4 color, float3 positionWS)
{
    // The full-screen fog passes already cover opaque particles.
    if (!IsSurfaceTypeTransparent())
    {
        return color;
    }

    SceneFog fog = SceneFogAt(positionWS);
    color.rgb = FogParticleLayer(color.rgb, color.a, fog.height);
    color.rgb = FogParticleLayer(color.rgb, color.a, fog.war);
    return color;
}

#endif
