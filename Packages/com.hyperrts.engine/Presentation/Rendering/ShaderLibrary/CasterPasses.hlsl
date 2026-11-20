// Shadow caster and depth-normals outputs shared by HyperRTS's hand-written shaders.
#ifndef HYPERRTS_CASTER_PASSES_INCLUDED
#define HYPERRTS_CASTER_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Set by URP while it renders shadow maps; punctual lights bias along the direction to each vertex.
float3 _LightDirection;
float3 _LightPosition;

// Clamped to the near plane so casters between the light and its near plane still cast.
float4 ShadowCasterPositionCS(float3 positionWS, float3 normalWS)
{
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 toLight = normalize(_LightPosition - positionWS);
    #else
        float3 toLight = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, toLight));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return positionCS;
}

// A unit normal as the depth-normals target stores it: octahedral when the renderer packs G-buffer normals.
half4 DepthNormalOutput(float3 normalWS)
{
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octahedral = PackNormalOctQuadEncode(normalWS);
        return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
    #else
        return half4(normalWS, 0.0);
    #endif
}

#endif
