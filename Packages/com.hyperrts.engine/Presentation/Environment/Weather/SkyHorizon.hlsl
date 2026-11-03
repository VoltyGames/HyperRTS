// The sky's colour as HyperRTS/Sky draws it, shared with the horizon's shaders so the far ground meets the sky's own
// horizon colour.
#ifndef HYPERRTS_SKY_HORIZON_INCLUDED
#define HYPERRTS_SKY_HORIZON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

// Skybox/Cubemap's unity_ColorSpaceDouble in linear space, so a grey tint of 0.5 matches it.
#define SKY_COLOR_SPACE_DOUBLE 4.59479380
// How far above the horizon the horizon colour is read, as a direction's height.
#define SKY_HORIZON_LIFT 0.02

// The cubemap's colour toward a direction in its own frame, at a mip.
half3 SkyColor(TEXTURECUBE_PARAM(cubemap, samplerCubemap), float4 decode, half3 tint, half exposure, float3 direction,
    float mip)
{
    half4 texel = SAMPLE_TEXTURECUBE_LOD(cubemap, samplerCubemap, direction, mip);
    return DecodeHDREnvironment(texel, decode) * tint * SKY_COLOR_SPACE_DOUBLE * exposure;
}

// The sky's horizon colour toward a unit bearing in the cubemap's frame, read just above the horizon at a blur mip.
half3 SkyHorizonColor(TEXTURECUBE_PARAM(cubemap, samplerCubemap), float4 decode, half3 tint, half exposure,
    float2 bearing, float blur)
{
    float3 lifted = normalize(float3(bearing.x, SKY_HORIZON_LIFT, bearing.y));
    return SkyColor(TEXTURECUBE_ARGS(cubemap, samplerCubemap), decode, tint, exposure, lifted, blur);
}

// A world bearing in the cubemap's frame: HyperRTS/Sky turns its dome by the rotation (degrees), so this turns back.
float2 SkyBearing(float2 worldXZ, float rotation)
{
    float angle = rotation * PI / 180.0;
    float2 turn = float2(cos(angle), sin(angle));
    return float2(worldXZ.x * turn.x + worldXZ.y * turn.y, -worldXZ.x * turn.y + worldXZ.y * turn.x);
}

#endif
