// HyperRTS/Terrain forward pass: the blended layers lit by URP's PBR model, with decals, SSAO and fog.
#ifndef HYPERRTS_TERRAIN_FORWARD_PASS_INCLUDED
#define HYPERRTS_TERRAIN_FORWARD_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayers.hlsl"

struct TerrainForwardVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half3 vertexSH : TEXCOORD3;
    half4 fogAndVertexLight : TEXCOORD4;
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord : TEXCOORD5;
    #endif
};

TerrainForwardVaryings TerrainForwardVertex(TerrainAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    TerrainInstancing(input);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    TerrainForwardVaryings output = (TerrainForwardVaryings)0;
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.uv = input.uv;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.vertexSH = SampleSHVertex(output.normalWS);

    half fog = 0.0;
    #if !defined(_FOG_FRAGMENT)
        fog = ComputeFogFactor(position.positionCS.z);
    #endif
    half3 vertexLight = 0.0;
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        vertexLight = VertexLighting(position.positionWS, output.normalWS);
    #endif
    output.fogAndVertexLight = half4(fog, vertexLight);

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(position);
    #endif
    return output;
}

InputData TerrainInputData(TerrainForwardVaryings input, half3 normalTS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    half3 normalWS = TerrainNormalWS(input.uv, input.normalWS);
    half3 tangentWS = normalize(cross(normalWS, half3(0.0, 0.0, 1.0)));
    inputData.tangentToWorld = half3x3(tangentWS, cross(tangentWS, normalWS), normalWS);
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, inputData.tangentToWorld));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif

    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogAndVertexLight.x);
    inputData.vertexLighting = input.fogAndVertexLight.yzw;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

    #if defined(_SCREEN_SPACE_IRRADIANCE)
        inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
    #else
        inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
    #endif
    return inputData;
}

SurfaceData TerrainSurfaceData(TerrainSurface surface)
{
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = surface.albedo;
    surfaceData.smoothness = surface.smoothness;
    surfaceData.normalTS = surface.normalTS;
    surfaceData.occlusion = surface.occlusion;
    surfaceData.alpha = 1.0;
    return surfaceData;
}

void TerrainForwardFragment(TerrainForwardVaryings input, out half4 outColor : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
    )
{
    ClipHoles(input.uv);
    TerrainSurface surface = BlendLayers(input.uv, input.positionWS);
    ApplyMacro(surface, input.uv, input.positionWS.xz);
    InputData inputData = TerrainInputData(input, surface.normalTS);
    SurfaceData surfaceData = TerrainSurfaceData(surface);
    #ifdef _DBUFFER
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
    #endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    outColor = half4(MixFog(color.rgb, inputData.fogCoord), 1.0);
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
