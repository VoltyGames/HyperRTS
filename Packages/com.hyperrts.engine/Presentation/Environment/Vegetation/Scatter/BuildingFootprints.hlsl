// BuildingFootprintMask for compute kernels and ground shaders. Declare sampler_LinearClamp before including.
#ifndef HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED
#define HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"

Texture2D<float> _HyperRTSFootprints;
float4 _HyperRTSFootprintRect; // x min, z min, 1 / width, 1 / depth; zero = no buildings

// 0 in the open to 1 under a building, eased across a cell at the footprint's edge.
float BuildingCover(float2 positionXZ)
{
    if (_HyperRTSFootprintRect.z <= 0.0)
    {
        return 0.0;
    }

    float2 uv = RectUV(_HyperRTSFootprintRect, positionXZ);
    return _HyperRTSFootprints.SampleLevel(sampler_LinearClamp, uv, 0);
}

bool UnderBuilding(float2 positionXZ)
{
    return BuildingCover(positionXZ) > 0.5;
}

#endif
