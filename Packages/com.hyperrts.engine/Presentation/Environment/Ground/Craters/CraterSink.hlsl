// Crater depth and rim height from tracks alpha, shared by the dented terrain and everything that sinks into it. Needs
// sampler_LinearClamp declared first.
#ifndef HYPERRTS_CRATER_SINK_INCLUDED
#define HYPERRTS_CRATER_SINK_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/TrampleGlobals.hlsl"

#define SINK_FULL_HEIGHT 0.5 // metres above the undented ground an object still sinks fully
#define SINK_NONE_HEIGHT 2.0 // and from where it stays put: aircraft, bridge decks, rooftops

// Published by CraterTerrain.cs, laid out as TerrainHeight.hlsl's _Height inputs.
Texture2D<float> _HyperRTSUndentedHeights;
float4 _HyperRTSUndentedArea;
float4 _HyperRTSUndentedScale;

// Metres the drawn ground sank at positionXZ, negative on a rim; zero off the maps.
float CraterDepth(float2 positionXZ)
{
    float2 uv = TrampleUV(positionXZ);
    float depth = 0.0;
    UNITY_BRANCH
    if (OnTrampleMaps(uv))
    {
        depth = GroundSunk(SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv, 0).a);
    }

    return depth;
}

// The same depth from a tracks texel the caller already fetched at positionXZ.
float CraterDepth(float2 positionXZ, float4 tracks)
{
    return OnTrampleMaps(TrampleUV(positionXZ)) ? GroundSunk(tracks.a) : 0.0;
}

#define SINK_RING_SAMPLES 8

// The depth a body rests at when it reaches radius metres from positionXZ: the shallowest ground on that ring, so a
// hull wider than a bowl stands across it instead of dropping into the ground beside it. Rims lift it as a point.
float BodyCraterDepth(float2 positionXZ, float radius)
{
    float depth = CraterDepth(positionXZ);
    UNITY_BRANCH
    if (radius > 0.0 && depth > 0.0)
    {
        for (int i = 0; i < SINK_RING_SAMPLES; i++)
        {
            float angle = i * (6.2831853 / SINK_RING_SAMPLES);
            float2 onRing = positionXZ + radius * float2(cos(angle), sin(angle));
            depth = min(depth, max(CraterDepth(onRing), 0.0));
        }
    }

    return depth;
}

float UndentedGroundHeight(float2 positionXZ)
{
    // Unpublished: flat ground at y = 0, as the simulation has without a terrain.
    float ground = 0.0;
    UNITY_BRANCH
    if (_HyperRTSUndentedScale.z >= 1.0)
    {
        ground = SampleHeightmap(_HyperRTSUndentedHeights, sampler_LinearClamp, _HyperRTSUndentedArea,
            _HyperRTSUndentedScale, positionXZ);
    }

    return ground;
}

// The whole object drops by the depth under its origin (rises on a rim), so it keeps its shape; raised objects move
// less or not at all. radius is how far the body it belongs to reaches, 0 for a point.
float CraterSink(float3 originWS, float radius)
{
    float above = originWS.y - UndentedGroundHeight(originWS.xz);
    return BodyCraterDepth(originWS.xz, radius) * (1.0 - smoothstep(SINK_FULL_HEIGHT, SINK_NONE_HEIGHT, above));
}

float3 SinkIntoCrater(float3 positionWS, float3 originWS)
{
    return positionWS - float3(0.0, CraterSink(originWS, 0.0), 0.0);
}

// Compute kernels have no object matrices.
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
float3 SinkObjectIntoCrater(float3 positionOS, float radius)
{
    float depth = CraterSink(TransformObjectToWorld(float3(0.0, 0.0, 0.0)), radius);
    return positionOS + mul((float3x3)GetWorldToObjectMatrix(), float3(0.0, -depth, 0.0));
}
#endif

#endif
