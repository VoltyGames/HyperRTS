// HyperRTS/Terrain shadow caster, depth, depth-normals and scene selection passes. Positions match the forward
// pass exactly; holes are cut in every pass.
#ifndef HYPERRTS_TERRAIN_DEPTH_PASSES_INCLUDED
#define HYPERRTS_TERRAIN_DEPTH_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct TerrainDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
};

TerrainDepthVaryings TerrainDepthVertex(TerrainAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    TerrainInstancing(input);
    TerrainDepthVaryings output;
    output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
    output.uv = input.uv;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 TerrainDepthFragment(TerrainDepthVaryings input) : SV_Target
{
    ClipHoles(input.uv);
    #ifdef SCENESELECTIONPASS
        return half4(_ObjectId, _PassValue, 1.0, 1.0);
    #else
        return input.positionCS.z;
    #endif
}

// The terrain's own normal, not the layers' detail: SSAO and decal angle fades work at the scale of the relief,
// and blending the layers again here would double the terrain's cost.
void TerrainDepthNormalsFragment(TerrainDepthVaryings input, out half4 outNormalWS : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
    )
{
    ClipHoles(input.uv);
    float3 normalWS = TerrainNormalWS(input.uv, input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octahedral = PackNormalOctQuadEncode(normalWS);
        outNormalWS = half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
    #else
        outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
    #endif
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

// Set by URP while it renders shadow maps; punctual lights bias along the direction to each vertex.
float3 _LightDirection;
float3 _LightPosition;

TerrainDepthVaryings TerrainShadowVertex(TerrainAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    TerrainInstancing(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    TerrainDepthVaryings output;
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    output.uv = input.uv;
    output.normalWS = normalWS;
    return output;
}

half4 TerrainShadowFragment(TerrainDepthVaryings input) : SV_Target
{
    ClipHoles(input.uv);
    return 0.0;
}

#endif
