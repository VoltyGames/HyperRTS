// Terrain height from its heightmap, bound by TerrainHeightInput.cs. Needs sampler_LinearClamp declared first.
#ifndef HYPERRTS_TERRAIN_HEIGHT_INCLUDED
#define HYPERRTS_TERRAIN_HEIGHT_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"

Texture2D<float> _Height;
float4 _HeightArea;  // terrain rect: x min, z min, 1 / size x, 1 / size z
float4 _HeightScale; // terrain base height, metres per unit sample, heightmap texels across

float GroundHeight(float2 xz)
{
    return SampleHeightmap(_Height, sampler_LinearClamp, _HeightArea, _HeightScale, xz);
}

#endif
