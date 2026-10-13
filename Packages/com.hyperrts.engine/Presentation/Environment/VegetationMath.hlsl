// Small math shared by wind, trample and plant shading. Plain HLSL, no textures.
#ifndef HYPERRTS_VEGETATION_MATH_INCLUDED
#define HYPERRTS_VEGETATION_MATH_INCLUDED

float WindHash(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float3 WindSafeNormalize(float3 v)
{
    return v * rsqrt(max(dot(v, v), 1e-8));
}

// Rodrigues rotation of p about a unit axis through pivot.
float3 WindRotate(float3 p, float3 pivot, float3 axis, float angle)
{
    float3 v = p - pivot;
    float s, c;
    sincos(angle, s, c);
    return pivot + v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

// UV in a packed world rect: xy min x and z, zw 1 / width and 1 / depth.
float2 RectUV(float4 rect, float2 positionXZ)
{
    return (positionXZ - rect.xy) * rect.zw;
}

// Smooth value noise in [0, 1] on a unit lattice.
float ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 t = smoothstep(0.0, 1.0, p - cell);
    float a = WindHash(float3(cell, 0.0));
    float b = WindHash(float3(cell + float2(1.0, 0.0), 0.0));
    float c = WindHash(float3(cell + float2(0.0, 1.0), 0.0));
    float d = WindHash(float3(cell + float2(1.0, 1.0), 0.0));
    return lerp(lerp(a, b, t.x), lerp(c, d, t.x), t.y);
}

#endif
