// Eye-space depth for perspective and orthographic cameras (map tools render top-down). Include after Core.hlsl.
#ifndef HYPERRTS_EYE_DEPTH_INCLUDED
#define HYPERRTS_EYE_DEPTH_INCLUDED

float EyeDepth(float rawDepth)
{
    return unity_OrthoParams.w == 0 ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
}

#endif
