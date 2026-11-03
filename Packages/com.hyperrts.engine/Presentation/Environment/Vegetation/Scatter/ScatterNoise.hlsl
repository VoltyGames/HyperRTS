// Seeded hashes and noise of the scatter kernels and the shaders that match them, as ScatterHash.cs has them.
#ifndef HYPERRTS_SCATTER_NOISE_INCLUDED
#define HYPERRTS_SCATTER_NOISE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

uint Hash(int seed, int row, int plant)
{
    return Pcg3(asuint(int3(seed, row, plant)));
}

// Smooth value noise from 0 to 1 on an integer grid.
float Noise(int seed, uint salt, float2 p)
{
    int salted = asint(asuint(seed) ^ salt);
    int2 cell = (int2)floor(p);
    float2 f = p - floor(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float low = lerp(Unit(Hash(salted, cell.x, cell.y)), Unit(Hash(salted, cell.x + 1, cell.y)), u.x);
    float high = lerp(Unit(Hash(salted, cell.x, cell.y + 1)), Unit(Hash(salted, cell.x + 1, cell.y + 1)), u.x);
    return lerp(low, high, u.y);
}

#endif
