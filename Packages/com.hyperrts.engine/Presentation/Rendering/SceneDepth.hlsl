// World position behind a full-screen pixel, for the fog passes. Needs URP Core.hlsl and DeclareDepthTexture.hlsl first.
#ifndef HYPERRTS_SCENE_DEPTH_INCLUDED
#define HYPERRTS_SCENE_DEPTH_INCLUDED

float3 ReconstructWorldPosition(float2 uv, float rawDepth)
{
    #if !UNITY_REVERSED_Z
        // OpenGL depth runs 0 to 1, the inverse projection wants the near clip value to 1.
        rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
    #endif
    return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
}

#endif
