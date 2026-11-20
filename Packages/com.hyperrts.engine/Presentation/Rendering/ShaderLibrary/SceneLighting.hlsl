// Ambient, sun, clustered-light and PBR setup shared by the custom lit shaders. Needs URP Lighting.hlsl first.
#ifndef HYPERRTS_SCENE_LIGHTING_INCLUDED
#define HYPERRTS_SCENE_LIGHTING_INCLUDED

// Surface Cache GI where it runs, else the ambient probe.
half3 SurfaceCacheAmbient(float3 positionWS, half3 normalWS, half3 viewDirWS, float2 positionSS)
{
    GIParams giParams = (GIParams)0;
    giParams.positionWS = positionWS;
    giParams.normalWS = normalWS;
    giParams.viewDirWS = viewDirWS;
    giParams.positionSS = positionSS;
    URP_LIGHT_ACCUM3 ambient;
    half4 shadowMask;
    InitializeBakedGI(giParams, ambient, shadowMask);
    return ambient;
}

// LIGHT_LOOP_BEGIN reads the cluster from a variable named inputData, so callers assign this to one.
InputData LightLoopInput(float3 positionWS, float2 normalizedScreenSpaceUV)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
    return inputData;
}

// A ground pixel's position, tangent basis, normal and screen position. Callers add the shadow coordinate, the GI
// and any vertex lighting, which differ between terrain and roads.
InputData GroundInputData(float3 positionWS, float4 positionCS, half3x3 tangentToWorld, half3 normalTS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
    inputData.geometricNormalWS = tangentToWorld[2];
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    return inputData;
}

// The sun's shadow and cookie at a point, with no baked shadow mask. The normal pushes the shadow lookup off the
// surface; billboards pass zero, having no surface to push along.
Light SunLight(float3 positionWS, float3 geometricNormalWS)
{
    const bool receiveShadows = true;
    const bool isSurfaceTypeTransparent = false;
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS, isSurfaceTypeTransparent, geometricNormalWS);
    return GetMainLight(shadowCoord, positionWS, half4(1.0, 1.0, 1.0, 1.0), receiveShadows, isSurfaceTypeTransparent);
}

// URP's PBR for the opaque ground and its decals: metallic workflow, no clear coat, shadows and reflections on.
URP_LIGHT_ACCUM4 GroundFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    const bool isSpecularSetup = false;
    const bool useSpecularHighlights = true;
    const bool useAlphaPremultiply = false;
    const bool useClearCoat = false;
    const bool receiveShadows = true;
    const bool isSurfaceTypeTransparent = false;
    const bool useEnvironmentReflections = true;
    return UniversalFragmentPBR(inputData, surfaceData,
        isSpecularSetup,
        useSpecularHighlights,
        useAlphaPremultiply,
        useClearCoat,
        receiveShadows,
        isSurfaceTypeTransparent,
        useEnvironmentReflections);
}

#endif
