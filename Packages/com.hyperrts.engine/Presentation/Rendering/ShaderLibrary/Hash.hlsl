// Integer hashes and value noise for shaders and compute. Plain HLSL; Pcg, Unit and Channel match ScatterHash.cs.
#ifndef HYPERRTS_HASH_INCLUDED
#define HYPERRTS_HASH_INCLUDED

// PCG hash (Jarzynski and Olano 2020): integer only, so every GPU gives the same bits, unlike a sine hash.
uint Pcg(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

uint Pcg3(uint3 value)
{
    return Pcg(value.x ^ Pcg(value.y ^ Pcg(value.z)));
}

// The top 24 bits, which a float holds exactly, as [0, 1).
float Unit(uint value)
{
    return (value >> 8) * (1.0 / 16777216.0);
}

// An independent [0, 1) value per channel from one hash.
float Channel(uint hash, uint channel)
{
    return Unit(Pcg(hash + channel * 0x9E3779B9u));
}

// A [0, 1) value from a position's bits: the same position always gives the same value.
float Random(float3 p)
{
    return Unit(Pcg3(asuint(p)));
}

float LatticeValue(int3 cell)
{
    return Unit(Pcg3(asuint(cell)));
}

// Smooth value noise in [0, 1] on a unit lattice.
float ValueNoise(float2 p)
{
    int2 cell = (int2)floor(p);
    float2 t = smoothstep(0.0, 1.0, p - floor(p));
    float a = LatticeValue(int3(cell, 0));
    float b = LatticeValue(int3(cell + int2(1, 0), 0));
    float c = LatticeValue(int3(cell + int2(0, 1), 0));
    float d = LatticeValue(int3(cell + int2(1, 1), 0));
    return lerp(lerp(a, b, t.x), lerp(c, d, t.x), t.y);
}

float ValueNoise(float3 p)
{
    int3 cell = (int3)floor(p);
    float3 t = smoothstep(0.0, 1.0, p - floor(p));
    float x00 = lerp(LatticeValue(cell), LatticeValue(cell + int3(1, 0, 0)), t.x);
    float x10 = lerp(LatticeValue(cell + int3(0, 1, 0)), LatticeValue(cell + int3(1, 1, 0)), t.x);
    float x01 = lerp(LatticeValue(cell + int3(0, 0, 1)), LatticeValue(cell + int3(1, 0, 1)), t.x);
    float x11 = lerp(LatticeValue(cell + int3(0, 1, 1)), LatticeValue(cell + int3(1, 1, 1)), t.x);
    return lerp(lerp(x00, x10, t.y), lerp(x01, x11, t.y), t.z);
}

#endif
