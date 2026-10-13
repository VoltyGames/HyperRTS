// Interaction maps published by InteractionSurfaces. Include after URP's Core.hlsl.
//   _HyperRTSTrample  RG travel direction * 0.5 + 0.5, B grass flatten, A crop damage
//   _HyperRTSTracks   R rut depth, G churned soil
// A zero rect (no map, or maps off) makes every guarded read neutral.
#ifndef HYPERRTS_TRAMPLE_INCLUDED
#define HYPERRTS_TRAMPLE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"

#define TRAMPLE_MAX_BEND 1.4 // radians a fully flattened plant leans over

TEXTURE2D(_HyperRTSTrample);
TEXTURE2D(_HyperRTSTracks);
float4 _HyperRTSInteractionRect; // x min, z min, 1 / width, 1 / depth; zero = no maps
float4 _HyperRTSTracksTexel;     // texels across and deep, metres per texel in x and z

struct TrampleSample
{
    float2 direction; // world XZ the trampler drove
    half flatten;     // grass: 1 flat, recovers to 0
    half crop;        // crops: 1 flattened or gone for good
};

struct TrackSample
{
    half depth;   // rut depth
    half churn;   // churned soil
    float2 slope; // rut depth per metre in x and z
};

bool InteractionMapsOn()
{
    return _HyperRTSInteractionRect.z > 0.0;
}

float2 InteractionUV(float2 positionXZ)
{
    return RectUV(_HyperRTSInteractionRect, positionXZ);
}

// Unguarded: the caller checks InteractionMapsOn. Vertex-safe (explicit LOD).
TrampleSample ReadTrample(float2 positionXZ)
{
    half4 texel = SAMPLE_TEXTURE2D_LOD(_HyperRTSTrample, sampler_LinearClamp, InteractionUV(positionXZ), 0);
    TrampleSample s;
    s.direction = texel.rg * 2.0 - 1.0;
    s.flatten = texel.b;
    s.crop = texel.a;
    return s;
}

// Vertex-safe (explicit LOD). Zero direction and amounts where there are no maps.
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

// Unguarded: the caller checks InteractionMapsOn. Slope is the bilinear surface's gradient over one 2x2 gather.
TrackSample ReadTracks(float2 positionXZ)
{
    float2 uv = InteractionUV(positionXZ);
    TrackSample s;
    half2 tracks = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv, 0).rg;
    s.depth = tracks.r;
    s.churn = tracks.g;

    // Gather order: x (u0, v1), y (u1, v1), z (u1, v0), w (u0, v0).
    float4 ruts = GATHER_RED_TEXTURE2D(_HyperRTSTracks, sampler_LinearClamp, uv);
    float2 t = frac(uv * _HyperRTSTracksTexel.xy - 0.5);
    float alongX = lerp(ruts.z - ruts.w, ruts.y - ruts.x, t.y);
    float alongZ = lerp(ruts.x - ruts.w, ruts.y - ruts.z, t.x);
    s.slope = float2(alongX, alongZ) / max(_HyperRTSTracksTexel.zw, 1e-4);
    return s;
}

// Leans a plant vertex about its root along the travel direction (0 standing, 1 flat). No direction just sinks.
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
    float3 axis = float3(d.y, 0.0, -d.x); // tips the top towards +direction
    return WindRotate(world, origin, axis, lean);
}

#endif
