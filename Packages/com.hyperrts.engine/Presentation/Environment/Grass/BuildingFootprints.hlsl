// BuildingFootprintMask for compute kernels. Declare sampler_LinearClamp before including.
#ifndef HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED
#define HYPERRTS_BUILDING_FOOTPRINTS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"

Texture2D<float> _HyperRTSFootprints;
float4 _HyperRTSFootprintRect; // x min, z min, 1 / width, 1 / depth; zero = no buildings

bool UnderBuilding(float2 positionXZ)
{
    if (_HyperRTSFootprintRect.z <= 0.0)
    {
        return false;
    }

    float2 uv = RectUV(_HyperRTSFootprintRect, positionXZ);
    return _HyperRTSFootprints.SampleLevel(sampler_LinearClamp, uv, 0) > 0.5;
}

#endif
