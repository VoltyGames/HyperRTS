// Interaction maps published by InteractionMap (InteractionSurfaces.cs). Include after URP's Core.hlsl.
//   _HyperRTSTrample  RG travel direction * 0.5 + 0.5, B grass flatten (recovers), A crop damage (permanent)
//   _HyperRTSTracks   R rut depth, G churned soil (both fade)
// Outside a map, or with the maps off, the rect is zero and every read here is neutral.
#ifndef HYPERRTS_TRAMPLE_INCLUDED
#define HYPERRTS_TRAMPLE_INCLUDED

#define TRAMPLE_MAX_BEND 1.4 // radians a fully flattened plant leans over

TEXTURE2D(_HyperRTSTrample);
TEXTURE2D(_HyperRTSTracks);
float4 _HyperRTSInteractionRect; // x min, z min, 1 / width, 1 / depth; zero = no maps
float4 _HyperRTSTracksTexel;     // 1 / texels across, 1 / texels deep, metres per texel in x and z

struct TrampleSample
{
    float2 direction; // world XZ the trampler drove, zero where none set it
    half flatten;     // grass: 1 flat, recovers to 0
    half crop;        // crops: 1 flattened or gone for good
};

bool InteractionMapsOn()
{
    return _HyperRTSInteractionRect.z > 0.0;
}

float2 InteractionUV(float2 positionXZ)
{
    return (positionXZ - _HyperRTSInteractionRect.xy) * _HyperRTSInteractionRect.zw;
}

// Vertex-safe (explicit LOD).
TrampleSample SampleTrample(float2 positionXZ)
{
    TrampleSample s = (TrampleSample)0;
    UNITY_BRANCH
    if (InteractionMapsOn())
    {
        half4 texel = SAMPLE_TEXTURE2D_LOD(_HyperRTSTrample, sampler_LinearClamp, InteractionUV(positionXZ), 0);
        s.direction = texel.rg * 2.0 - 1.0;
        s.flatten = texel.b;
        s.crop = texel.a;
    }
    return s;
}

// Vertex-safe (explicit LOD). x rut depth, y churned soil.
half2 SampleTracks(float2 positionXZ)
{
    half2 tracks = 0.0;
    UNITY_BRANCH
    if (InteractionMapsOn())
    {
        tracks = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, InteractionUV(positionXZ), 0).rg;
    }
    return tracks;
}

// Rut depth slope per metre in x and z, from the neighbouring texels.
float2 TrackSlope(float2 positionXZ)
{
    float2 step = _HyperRTSTracksTexel.zw;
    float east = SampleTracks(positionXZ + float2(step.x, 0.0)).r;
    float west = SampleTracks(positionXZ - float2(step.x, 0.0)).r;
    float north = SampleTracks(positionXZ + float2(0.0, step.y)).r;
    float south = SampleTracks(positionXZ - float2(0.0, step.y)).r;
    return float2(east - west, north - south) / max(2.0 * step, 1e-4);
}

// Leans a plant vertex flat about its root along the travel direction by amount (0 standing, 1 flat). Plants with
// no direction under them just sink. Row crops and grass share it.
float3 TrampleBend(float3 world, float3 origin, float2 direction, half amount)
{
    float lean = amount * TRAMPLE_MAX_BEND;
    float3 v = world - origin;
    float s, c;
    sincos(lean, s, c);
    float length2 = dot(direction, direction);
    if (length2 < 1e-4)
    {
        return origin + float3(v.x, v.y * c, v.z);
    }

    float2 d = direction * rsqrt(length2);
    float3 axis = float3(d.y, 0.0, -d.x); // tips the top towards +direction
    return origin + v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

#endif
