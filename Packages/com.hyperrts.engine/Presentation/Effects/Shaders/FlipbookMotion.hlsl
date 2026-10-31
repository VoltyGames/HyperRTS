// Flipbook frames moved along a motion map, so a cross-fade follows the smoke instead of ghosting. Needs URP Core.hlsl first.
#ifndef HYPERRTS_FLIPBOOK_MOTION_INCLUDED
#define HYPERRTS_FLIPBOOK_MOTION_INCLUDED

// The largest move a frame step holds, in sheet UV; motion map generators encode against the same value.
#define FLIPBOOK_MOTION_RANGE (1.0 / 64.0)

// RG centred on 0.5: R moves along U, G along V, toward the next frame.
float2 FlipbookMotion(TEXTURE2D_PARAM(motionMap, motionSampler), float2 uv)
{
    return (SAMPLE_TEXTURE2D(motionMap, motionSampler, uv).rg * 2.0 - 1.0) * FLIPBOOK_MOTION_RANGE;
}

// Pushes the current frame forward and pulls the next one back by the blend, so both meet where the smoke is.
void FlipbookMotionWarp(TEXTURE2D_PARAM(motionMap, motionSampler), inout float2 current, inout float2 next, float blend)
{
    float2 currentMotion = FlipbookMotion(TEXTURE2D_ARGS(motionMap, motionSampler), current);
    float2 nextMotion = FlipbookMotion(TEXTURE2D_ARGS(motionMap, motionSampler), next);
    current -= currentMotion * blend;
    next += nextMotion * (1.0 - blend);
}

#endif
