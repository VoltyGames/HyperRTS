// URP's SurfaceData for a TerrainSurface, so terrain and roads light alike. Needs Lighting.hlsl first.
#ifndef HYPERRTS_TERRAIN_SURFACE_DATA_INCLUDED
#define HYPERRTS_TERRAIN_SURFACE_DATA_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainSurface.hlsl"

SurfaceData TerrainSurfaceData(TerrainSurface surface)
{
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = surface.albedo;
    surfaceData.smoothness = surface.smoothness;
    surfaceData.normalTS = surface.normalTS;
    surfaceData.occlusion = surface.occlusion;
    surfaceData.alpha = 1.0;
    return surfaceData;
}

#endif
