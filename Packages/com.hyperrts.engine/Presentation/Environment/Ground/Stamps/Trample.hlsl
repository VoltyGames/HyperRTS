// Reads the trample maps (ruts, craters, scorch, stains) for ground, water and plants. Include after URP's Core.hlsl.
#ifndef HYPERRTS_TRAMPLE_INCLUDED
#define HYPERRTS_TRAMPLE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/TrampleGlobals.hlsl"

#define TRAMPLE_MAX_BEND 1.4
#define CRATER_TAP_TEXELS 1.5
#define CRATER_MIN_SLOPE_DEPTH 0.01 // metres: under a quarter of one 8-bit step, so level ground skips the taps

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
    float crater; // metres the ground sank, negative on a rim
    float2 craterSlope;
};

// Unguarded: the caller checks HasTrampleMaps.
TrampleSample ReadTrample(float2 positionXZ)
{
    half4 texel = SAMPLE_TEXTURE2D_LOD(_HyperRTSTrample, sampler_LinearClamp, TrampleUV(positionXZ), 0);
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
    if (HasTrampleMaps())
    {
        s = ReadTrample(positionXZ);
    }
    return s;
}

// The stamped ramp, cut at RUT_EDGE with a screen-space anti-aliased edge: crisp bands from texels a few times wider.
// Needs screen derivatives, so call it from a pixel shader.
half RutCoverage(float ramp)
{
    float width = max(fwidth(ramp), 0.04);
    float inside = saturate((ramp - RUT_EDGE) / width + 0.5);
    return (half)(inside * saturate(ramp / RUT_FULL));
}

// Unguarded: the caller checks HasTrampleMaps.
TrackSample ReadTracks(float2 positionXZ)
{
    float2 uv = TrampleUV(positionXZ);
    TrackSample s;
    // Full precision: the ground level sits at a bias that half would blur.
    float4 tracks = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv, 0);
    s.depth = RutCoverage(tracks.r / RUT_LONE_SHARE);
    s.churn = tracks.g;
    s.scorch = tracks.b;
    s.crater = GroundSunk(tracks.a);
    s.craterSlope = 0.0;

    // Gather order: x (u0, v1), y (u1, v1), z (u1, v0), w (u0, v0).
    float4 ruts = GATHER_RED_TEXTURE2D(_HyperRTSTracks, sampler_LinearClamp, uv) / RUT_LONE_SHARE;
    float2 t = frac(uv * _HyperRTSTracksTexel.xy - 0.5);
    float alongX = lerp(ruts.z - ruts.w, ruts.y - ruts.x, t.y);
    float alongZ = lerp(ruts.x - ruts.w, ruts.y - ruts.z, t.x);
    s.slope = float2(alongX, alongZ) / max(_HyperRTSTracksTexel.zw, 1e-4);

    // A bowl spans many texels, so a wide central difference keeps its slope smooth.
    UNITY_BRANCH
    if (abs(s.crater) > CRATER_MIN_SLOPE_DEPTH)
    {
        float2 step = CRATER_TAP_TEXELS / max(_HyperRTSTracksTexel.xy, 1.0);
        float east = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv + float2(step.x, 0.0), 0).a;
        float west = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv - float2(step.x, 0.0), 0).a;
        float north = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv + float2(0.0, step.y), 0).a;
        float south = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, uv - float2(0.0, step.y), 0).a;
        float2 span = 2.0 * CRATER_TAP_TEXELS * max(_HyperRTSTracksTexel.zw, 1e-4);
        s.craterSlope = float2(east - west, north - south) * HYPERRTS_GROUND_RANGE / span;
    }
    return s;
}

// Guarded: zero without maps. Read the scorch from .b, and pass it to CraterDepth for the depth from .a.
float4 SampleTracks(float2 positionXZ)
{
    float4 tracks = 0.0;
    UNITY_BRANCH
    if (HasTrampleMaps())
    {
        tracks = SAMPLE_TEXTURE2D_LOD(_HyperRTSTracks, sampler_LinearClamp, TrampleUV(positionXZ), 0);
    }
    return tracks;
}

half SampleWake(float3 positionWS)
{
    half wake = 0.0;
    UNITY_BRANCH
    if (HasTrampleMaps())
    {
        wake = SAMPLE_TEXTURE2D_LOD(_HyperRTSWake, sampler_LinearClamp, TrampleUV(positionWS.xz), 0).r;
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
    return RotateAbout(world, origin, axis, lean);
}

#endif
