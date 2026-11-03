// Jobs, band edges, terrain height and the slot layout and counting shared by RowCrops.compute and Grass.compute, with
// ScatterNoise.hlsl's hashes and noise. The job matches ScatterJob.cs and the band edges ScatterBands.cs; ScatterDraws.cs
// binds the jobs, counters and args and ScatterSlots.cs the slots.
#ifndef HYPERRTS_SCATTER_INCLUDED
#define HYPERRTS_SCATTER_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/TerrainHeight.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/ScatterNoise.hlsl"

struct DrawArgs // GraphicsBuffer.IndirectDrawIndexedArgs, 20 bytes
{
    uint indexCount;
    uint instanceCount;
    uint startIndex;
    int baseVertex;
    uint startInstance;
};

struct ScatterJob // ScatterJob.cs, 28 bytes
{
    float2 tileMin;
    int id;     // the plant or field this job grows
    int2 first; // first lattice spot that can fall in the tile
    int2 count; // spots from first
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

#define BAND_EDGE_WIDTH 0.4 // ScatterBands.EdgeWidth

// The range a plant picks its distance band at, off by up to half the edge width either way, so neighbouring bands
// mix over a stretch instead of meeting at a line that sweeps across the ground as the camera zooms.
float BandRange(float range, float pick)
{
    return range * (1.0 + (pick - 0.5) * BAND_EDGE_WIDTH);
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
