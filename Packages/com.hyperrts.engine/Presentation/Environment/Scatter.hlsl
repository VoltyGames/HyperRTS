// Hashes, noise, terrain height and the slot counting shared by the plant scatter kernels, RowCrops.compute and
// Grass.compute. The hashes and noise match RowCropSpots.cs; the counters and args are bound by ScatterDraws.cs.
#ifndef HYPERRTS_SCATTER_INCLUDED
#define HYPERRTS_SCATTER_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainHeight.hlsl"

struct DrawArgs // GraphicsBuffer.IndirectDrawIndexedArgs, 20 bytes
{
    uint indexCount;
    uint instanceCount;
    uint startIndex;
    int baseVertex;
    uint startInstance;
};

RWStructuredBuffer<uint> _Counters; // plants grown per slot this run
RWStructuredBuffer<DrawArgs> _Args;
float _TileSize;

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

// Each spot belongs to one tile, so plants on a tile edge aren't grown twice.
bool Owns(float2 tileMin, float2 spot)
{
    float2 tileMax = tileMin + _TileSize;
    return all(spot >= tileMin) && all(spot < tileMax);
}

// Takes the slot's next index; false once the slot is full, as plants past its capacity are dropped.
bool Claim(uint slot, uint capacity, out uint index)
{
    InterlockedAdd(_Counters[slot], 1u, index);
    return index < capacity;
}

// Draws a slot's grown plants up to its capacity, then zeroes its counter for the next run.
void CloseSlot(uint slot, uint capacity)
{
    _Args[slot].instanceCount = min(_Counters[slot], capacity);
    _Counters[slot] = 0u;
}

#endif
