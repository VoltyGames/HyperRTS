// Include after URP's Core.hlsl.
#ifndef HYPERRTS_TRAMPLE_INCLUDED
#define HYPERRTS_TRAMPLE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/InteractionGlobals.hlsl"

#define TRAMPLE_MAX_BEND 1.4
#define CRATER_TAP_TEXELS 1.5

struct TrampleSample
{
    float2 direction;
    half flatten;
    half crop;
};

struct TrackSample
{
    half depth;
    half churn;
    float2 slope;
    half scorch;
    float crater; // metres
    float2 craterSlope;
};

bool InteractionMapsOn()
{
    return _HyperRTSInteractionRect.z > 0.0;
}

float2 InteractionUV(float2 positionXZ)
{
    return RectUV(_HyperRTSInteractionRect, positionXZ);
}

// Unguarded: the caller checks InteractionMapsOn.
TrampleSample ReadTrample(float2 positionXZ)
{
    half4 texel = SAMPLE_TEXTURE2D_LOD(_HyperRTSTrample, sampler_LinearClamp, InteractionUV(positionXZ), 0);
    TrampleSample s;
    s.direction = texel.rg * 2.0 - 1.0;
    s.flatten = texel.b;
    s.crop = texel.a;
    return s;
}

TrampleSample SampleTrample(float2 positionXZ)
{
    TrampleSample s = (TrampleSample)0;
    UNITY_BRANCH
    if (InteractionMapsOn())
    {
        s = ReadTrample(positionXZ);
    }
    return s;
}

// Unguarded: the caller checks InteractionMapsOn.
TrackSample ReadTracks(float2 positionXZ)
{
    float2 uv = InteractionUV(positionXZ);
    TrackSample s;
    half4 tracks = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv, 0);
    s.depth = tracks.r;
    s.churn = tracks.g;
    s.scorch = tracks.b;
    s.crater = tracks.a * _HyperRTSMaxCraterDepth;
    s.craterSlope = 0.0;

    // Gather order: x (u0, v1), y (u1, v1), z (u1, v0), w (u0, v0).
    float4 ruts = GATHER_RED_TEXTURE2D(_HyperRTSTracks, sampler_LinearClamp, uv);
    float2 t = frac(uv * _HyperRTSTracksTexel.xy - 0.5);
    float alongX = lerp(ruts.z - ruts.w, ruts.y - ruts.x, t.y);
    float alongZ = lerp(ruts.x - ruts.w, ruts.y - ruts.z, t.x);
    s.slope = float2(alongX, alongZ) / max(_HyperRTSTracksTexel.zw, 1e-4);

    // A bowl spans many texels, so a wide central difference keeps its slope smooth.
    UNITY_BRANCH
    if (tracks.a > 0.0)
    {
        float2 step = CRATER_TAP_TEXELS / max(_HyperRTSTracksTexel.xy, 1.0);
        float east = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv + float2(step.x, 0.0), 0).a;
        float west = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv - float2(step.x, 0.0), 0).a;
        float north = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv + float2(0.0, step.y), 0).a;
        float south = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv - float2(0.0, step.y), 0).a;
        float2 span = 2.0 * CRATER_TAP_TEXELS * max(_HyperRTSTracksTexel.zw, 1e-4);
        s.craterSlope = float2(east - west, north - south) * _HyperRTSMaxCraterDepth / span;
    }
    return s;
}

half SampleWake(float3 positionWS)
{
    half wake = 0.0;
    UNITY_BRANCH
    if (InteractionMapsOn())
    {
        wake = SAMPLE_TEXTURE2D_LOD(_HyperRTSWake, sampler_LinearClamp, InteractionUV(positionWS.xz), 0).r;
    }
    return wake;
}

// With no direction a plant just sinks.
float3 TrampleBend(float3 world, float3 origin, float2 direction, half amount)
{
    float lean = amount * TRAMPLE_MAX_BEND;
    float length2 = dot(direction, direction);
    if (length2 < 1e-4)
    {
        float3 v = world - origin;
        return origin + float3(v.x, v.y * cos(lean), v.z);
    }

    float2 d = direction * rsqrt(length2);
    float3 axis = float3(d.y, 0.0, -d.x);
    return WindRotate(world, origin, axis, lean);
}

#endif
