// Ambient and clustered-light setup shared by the custom lit shaders. Needs URP Lighting.hlsl first.
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

#endif
