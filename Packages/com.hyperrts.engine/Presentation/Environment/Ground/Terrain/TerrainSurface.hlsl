// The pixel and surface the terrain's layers and stains work on; roads fill them in too, to take the same stains.
#ifndef HYPERRTS_TERRAIN_SURFACE_INCLUDED
#define HYPERRTS_TERRAIN_SURFACE_INCLUDED

struct TerrainSurface
{
    half3 albedo;
    half3 normalTS;    // terrain tangent space: x east, y north, z along the terrain normal
    half smoothness;
    half occlusion;
    half height;
    half tintStrength;
    half grass;
};

struct TerrainPixel
{
    float2 xz;
    float2 dx;
    float2 dy;
    float3 positionWS;
    float3 dPdx;
    float3 dPdy;
    half3x3 frame;     // TerrainFrame of the terrain's own normal
    float distance;    // to the camera
    half far;          // the far sample's share of albedo
    half relief;       // share of the layers' normal detail kept at this distance
    half side;         // the side projections' share for triplanar layers
    half2 sideWeights; // x and z projections' split of that share
    float2 rowTurn;    // (cos, sin) that lays u along this pixel's crop rows
};

#endif
