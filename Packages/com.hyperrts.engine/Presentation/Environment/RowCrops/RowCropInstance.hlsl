// One planted row crop, written by RowCrops.compute and drawn by RowCrop.shader: 48 bytes, RowCropBatch.InstanceStride.
#ifndef HYPERRTS_ROW_CROP_INSTANCE_INCLUDED
#define HYPERRTS_ROW_CROP_INSTANCE_INCLUDED

struct RowCropInstance
{
    float3 position;
    float scale;
    float2 facing;
    float2 trampleDirection;
    float bend;
    float shade;
    float2 lean; // XZ direction times the sine of the tilt
};

#endif
