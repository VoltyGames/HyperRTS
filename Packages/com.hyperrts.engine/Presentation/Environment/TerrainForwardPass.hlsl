#ifndef HYPERRTS_TERRAIN_FORWARD_PASS_INCLUDED
#define HYPERRTS_TERRAIN_FORWARD_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayers.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainMicro.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainWetness.hlsl"

#define RUT_DEPTH 0.12
#define RUT_DARKENING 0.4
#define SOIL_TINT 0.65
#define FLATTEN_DARKENING 0.06
#define CRATER_RELIEF 1.6       // exaggerated so the sun shapes the bowl from the RTS camera
#define CRATER_FULL_DEPTH 0.5
#define CRATER_DARKENING 0.3
#define CRATER_DAMPNESS 0.2
#define CRATER_OCCLUSION 0.35
#define CRATER_PUDDLE 0.6
#define SCORCH_COVER 0.9
#define SCORCH_SMOOTHNESS 0.03

struct TerrainForwardVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half3 vertexSH : TEXCOORD3;
    half3 vertexLight : TEXCOORD4;
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

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        output.vertexLight = VertexLighting(position.positionWS, output.normalWS);
    #endif

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(position);
    #endif
    return output;
}

// Returns where puddles gather.
half ApplyInteraction(inout TerrainSurface surface, float2 positionXZ)
{
    UNITY_BRANCH
    if (!InteractionMapsOn())
    {
        return 0.0;
    }

    TrackSample tracks = ReadTracks(positionXZ);
    half flatten = ReadTrample(positionXZ).flatten;
    half bowl = (half)saturate(tracks.crater / CRATER_FULL_DEPTH);
    half3 earth = surface.albedo * half3(0.62, 0.5, 0.4);
    surface.albedo = lerp(surface.albedo, earth, SOIL_TINT * max(tracks.churn, bowl));
    surface.albedo *= (1.0 - RUT_DARKENING * tracks.depth) * (1.0 - FLATTEN_DARKENING * flatten) *
        (1.0 - CRATER_DARKENING * bowl);
    surface.smoothness = saturate(surface.smoothness + CRATER_DAMPNESS * bowl);
    surface.occlusion *= 1.0 - CRATER_OCCLUSION * bowl;

    surface.albedo = lerp(surface.albedo, half3(0.045, 0.04, 0.035), SCORCH_COVER * tracks.scorch);
    surface.smoothness = lerp(surface.smoothness, SCORCH_SMOOTHNESS, tracks.scorch);

    half2 slope = (half2)(tracks.slope * RUT_DEPTH + tracks.craterSlope * CRATER_RELIEF);
    surface.normalTS = normalize(half3(surface.normalTS.xy + slope, surface.normalTS.z));
    return max(tracks.depth, CRATER_PUDDLE * bowl);
}

InputData TerrainInputData(TerrainForwardVaryings input, TerrainPixel pixel, half3 normalTS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.tangentToWorld = pixel.frame;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, inputData.tangentToWorld));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif

    inputData.vertexLighting = input.vertexLight;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    GIParams giParams = (GIParams)0;
    giParams.vertexSH = input.vertexSH;
    giParams.positionWS = inputData.positionWS;
    giParams.normalWS = inputData.normalWS;
    giParams.viewDirWS = inputData.viewDirectionWS;
    giParams.positionSS = input.positionCS.xy;
    InitializeBakedGI(giParams, inputData.bakedGI, inputData.shadowMask);
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
    TerrainPixel pixel = GetTerrainPixel(input.uv, input.positionWS, TerrainNormalWS(input.uv, input.normalWS));
    TerrainSurface surface = BlendLayers(input.uv, pixel);
    #ifdef TERRAIN_MICRO
        ApplyMicro(surface, pixel);
    #endif
    half dampness = ApplyGroundTint(surface, input.uv, input.positionWS.xz);
    half rut = ApplyInteraction(surface, input.positionWS.xz);
    ApplyWetness(surface, dampness, rut, input.positionWS.xz);
    InputData inputData = TerrainInputData(input, pixel, surface.normalTS);
    SurfaceData surfaceData = TerrainSurfaceData(surface);
    #ifdef _DBUFFER
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
    #endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    outColor = half4(color.rgb, 1.0);
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
