// Crater depth from tracks alpha, shared by the dented terrain and everything that sinks into it. Needs
// sampler_LinearClamp declared first.
#ifndef HYPERRTS_CRATER_SINK_INCLUDED
#define HYPERRTS_CRATER_SINK_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/InteractionGlobals.hlsl"

#define SINK_FULL_HEIGHT 0.5 // metres above the undented ground an object still sinks fully
#define SINK_NONE_HEIGHT 2.0 // and from where it stays put: aircraft, bridge decks, rooftops

// Published by CraterTerrain.cs, laid out as TerrainHeight.hlsl's _Height inputs.
Texture2D<float> _HyperRTSUndentedHeights;
float4 _HyperRTSUndentedArea;
float4 _HyperRTSUndentedScale;

// Metres the drawn ground sank at positionXZ; none off the maps, where CraterTerrainSystem dents nothing.
float CraterDepth(float2 positionXZ)
{
    float2 uv = RectUV(_HyperRTSInteractionRect, positionXZ);
    bool onMaps = _HyperRTSInteractionRect.z > 0.0 && all(uv == saturate(uv));
    UNITY_BRANCH
    if (!onMaps)
    {
        return 0.0;
    }

    return SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv, 0).a * _HyperRTSMaxCraterDepth;
}

float UndentedGroundHeight(float2 positionXZ)
{
    float texels = _HyperRTSUndentedScale.z;
    // Unpublished: flat ground at y = 0, as the simulation has without a terrain.
    UNITY_BRANCH
    if (texels < 1.0)
    {
        return 0.0;
    }

    float2 uv = (positionXZ - _HyperRTSUndentedArea.xy) * _HyperRTSUndentedArea.zw;
    uv = uv * ((texels - 1.0) / texels) + 0.5 / texels;
    float height = _HyperRTSUndentedHeights.SampleLevel(sampler_LinearClamp, uv, 0);
    return _HyperRTSUndentedScale.x + height * _HyperRTSUndentedScale.y;
}

// The whole object drops by the depth at its origin, so it keeps its shape; raised objects drop less or not at all.
float CraterSink(float3 originWS)
{
    float above = originWS.y - UndentedGroundHeight(originWS.xz);
    return CraterDepth(originWS.xz) * (1.0 - smoothstep(SINK_FULL_HEIGHT, SINK_NONE_HEIGHT, above));
}

float3 SinkIntoCrater(float3 positionWS, float3 originWS)
{
    return positionWS - float3(0.0, CraterSink(originWS), 0.0);
}

// Compute kernels have no object matrices.
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
float3 SinkObjectIntoCrater(float3 positionOS)
{
    float depth = CraterSink(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
    return positionOS + mul((float3x3)GetWorldToObjectMatrix(), float3(0.0, -depth, 0.0));
}
#endif

#endif
