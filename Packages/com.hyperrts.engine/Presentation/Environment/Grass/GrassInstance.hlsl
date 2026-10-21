// One scattered plant, written by Grass.compute and drawn by Hidden/HyperRTS/FoliageGrass: 48 bytes, GrassBatch.InstanceStride.
#ifndef HYPERRTS_GRASS_INSTANCE_INCLUDED
#define HYPERRTS_GRASS_INSTANCE_INCLUDED

struct GrassInstance
{
    float3 position;
    float width;
    float height;
    float yaw;
    float2 lean; // XZ direction times the sine of the tilt
    float3 ground; // ground colour under the plant over the colour round it
    float shade;
};

#endif
