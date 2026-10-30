// Plants burnt away where ash lies in the remains map. Needs sampler_LinearClamp declared first.
#ifndef HYPERRTS_BURNT_PLANTS_INCLUDED
#define HYPERRTS_BURNT_PLANTS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/InteractionGlobals.hlsl"

#define ASH_CLEARS 0.7 // ash at which every plant is gone; a crater's lighter ash only thins them

// Share of plants burnt away at positionXZ, compared against a per-plant random; none off the maps.
float BurntShare(float2 positionXZ)
{
    float2 uv = RectUV(_HyperRTSInteractionRect, positionXZ);
    bool onMaps = _HyperRTSInteractionRect.z > 0.0 && all(uv == saturate(uv));
    float ash = SAMPLE_TEXTURE2D_LOD(_HyperRTSRemains, sampler_LinearClamp, saturate(uv), 0).g;
    return onMaps ? saturate(ash / ASH_CLEARS) : 0.0;
}

#endif
