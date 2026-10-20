// HyperRTS/Terrain shadow caster, depth, depth-normals and scene selection passes. Positions match the forward
// pass exactly; holes are cut in every pass.
#ifndef HYPERRTS_TERRAIN_DEPTH_PASSES_INCLUDED
#define HYPERRTS_TERRAIN_DEPTH_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

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
    outNormalWS = DepthNormalOutput(NormalizeNormalPerPixel(TerrainNormalWS(input.uv, input.normalWS)));
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

TerrainDepthVaryings TerrainShadowVertex(TerrainAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    TerrainInstancing(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    TerrainDepthVaryings output;
    output.positionCS = ShadowCasterPositionCS(positionWS, normalWS);
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
