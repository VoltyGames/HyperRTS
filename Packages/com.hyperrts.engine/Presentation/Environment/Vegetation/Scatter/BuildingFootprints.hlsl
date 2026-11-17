// BuildingFootprintMask for compute kernels and ground shaders. Declare sampler_LinearClamp before including.
#ifndef HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED
#define HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"

Texture2D<float> _HyperRTSFootprints;
float4 _HyperRTSFootprintRect; // x min, z min, 1 / width, 1 / depth; zero = no buildings

// The mask's soft cover: 1 inside a building's box, 0 beyond its margin.
float BuildingMargin(float2 positionXZ)
{
    if (_HyperRTSFootprintRect.z <= 0.0)
    {
        return 0.0;
    }

    float2 uv = RectUV(_HyperRTSFootprintRect, positionXZ);
    return _HyperRTSFootprints.SampleLevel(sampler_LinearClamp, uv, 0);
}

// 0 in the open to 1 inside the box: the margin is cut away, so roads still run up to a yard.
float BuildingCover(float2 positionXZ)
{
    return saturate((BuildingMargin(positionXZ) - 0.9) * 10.0);
}

// Whether a plant with a random 0..1 stands in a building's yard; the jitter ragged-edges the margin.
bool NearBuilding(float2 positionXZ, float random)
{
    return BuildingMargin(positionXZ) > random * 0.15;
}

#endif
