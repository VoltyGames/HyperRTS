// Hashes, noise and terrain height shared by the plant scatter kernels, RowCrops.compute and Grass.compute.
// The hashes and noise match RowCropLattice.cs; the heightmap inputs are bound by TerrainHeightInput.cs.
#ifndef HYPERRTS_SCATTER_INCLUDED
#define HYPERRTS_SCATTER_INCLUDED

Texture2D<float> _Height;
float4 _HeightArea;  // terrain rect: x min, z min, 1 / size x, 1 / size z
float4 _HeightScale; // terrain base height, metres per unit sample, heightmap texels across

uint Pcg(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

uint Hash(int seed, int row, int plant)
{
    return Pcg(asuint(seed) ^ Pcg(asuint(row) ^ Pcg(asuint(plant))));
}

float Unit(uint value)
{
    return (value >> 8) * (1.0 / 16777216.0);
}

float Channel(uint hash, uint channel)
{
    return Unit(Pcg(hash + channel * 0x9E3779B9u));
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

// Needs sampler_LinearClamp declared by the kernel.
float GroundHeight(float2 xz)
{
    float texels = _HeightScale.z;
    float2 uv = (xz - _HeightArea.xy) * _HeightArea.zw;
    uv = uv * ((texels - 1.0) / texels) + 0.5 / texels;
    return _HeightScale.x + _Height.SampleLevel(sampler_LinearClamp, uv, 0) * _HeightScale.y;
}

#endif
