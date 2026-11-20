// Shared by RowCrops.compute and Grass.compute: jobs (a visible tile of one plant to fill), seeded hashes and noise,
// distance bands, terrain height, and slots (one output range per plant model). Hashes and noise match ScatterHash.cs,
// band edges ScatterBands.cs, slots ScatterSlots.cs.
#ifndef HYPERRTS_SCATTER_INCLUDED
#define HYPERRTS_SCATTER_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainHeight.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Hash.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/ScatterJob.cs.hlsl"

struct DrawArgs // GraphicsBuffer.IndirectDrawIndexedArgs, 20 bytes
{
    uint indexCount;
    uint instanceCount;
    uint startIndex;
    int baseVertex;
    uint startInstance;
};

struct ScatterSlot // ScatterSlots.cs: where a slot writes in _Instances and how many it holds
{
    uint offset;
    uint capacity;
};

StructuredBuffer<ScatterJob> _Jobs;
StructuredBuffer<ScatterSlot> _Slots;
uint _SlotCount;
RWStructuredBuffer<uint> _Counters; // plants grown per slot this run
RWStructuredBuffer<DrawArgs> _Args;
float _TileSize;

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

#define BAND_EDGE_WIDTH 0.4 // ScatterBands.EdgeWidth

// The range a plant picks its distance band at, off by up to half the edge width either way, so neighbouring bands
// mix over a stretch instead of meeting at a line that sweeps across the ground as the camera zooms.
float BandRange(float range, float pick)
{
    return range * (1.0 + (pick - 0.5) * BAND_EDGE_WIDTH);
}

// The job this thread works on; false for threads past its last cell.
bool JobAt(uint3 id, out ScatterJob job)
{
    job = _Jobs[id.z];
    return id.x < (uint)(job.count.x * job.count.y);
}

// The lattice cell a job's thread index lands on, row by row.
int2 JobCell(ScatterJob job, uint index)
{
    return job.first + int2(index % (uint)job.count.x, index / (uint)job.count.x);
}

// Each spot belongs to one tile, so plants on a tile edge aren't grown twice.
bool Owns(float2 tileMin, float2 spot)
{
    float2 tileMax = tileMin + _TileSize;
    return all(spot >= tileMin) && all(spot < tileMax);
}

// Takes the slot's next place in _Instances; false once the slot is full, as plants past its capacity are dropped.
bool Claim(uint slot, out uint index)
{
    ScatterSlot target = _Slots[slot];
    uint taken;
    InterlockedAdd(_Counters[slot], 1u, taken);
    index = target.offset + taken;
    return taken < target.capacity;
}

// Draws every slot's grown plants up to its capacity, then zeroes the counters for the next run.
void CloseSlots()
{
    for (uint slot = 0u; slot < _SlotCount; slot++)
    {
        _Args[slot].instanceCount = min(_Counters[slot], _Slots[slot].capacity);
        _Counters[slot] = 0u;
    }
}

#endif
