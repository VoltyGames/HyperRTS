// Flipbook frames moved along a motion map, so a cross-fade follows the smoke instead of ghosting. Needs URP Core.hlsl first.
#ifndef HYPERRTS_FLIPBOOK_MOTION_INCLUDED
#define HYPERRTS_FLIPBOOK_MOTION_INCLUDED

TEXTURE2D(_MotionMap);
SAMPLER(sampler_MotionMap);

// The largest move a frame step holds, in sheet UV; motion map generators encode against the same value.
#define FLIPBOOK_MOTION_RANGE (1.0 / 64.0)

// RG centred on 0.5: R moves along U, G along V, toward the next frame.
float2 FlipbookMotion(float2 uv)
{
    return (SAMPLE_TEXTURE2D(_MotionMap, sampler_MotionMap, uv).rg * 2.0 - 1.0) * FLIPBOOK_MOTION_RANGE;
}

// Pushes the current frame forward and pulls the next one back by the blend, so both meet where the smoke is.
void FlipbookMotionWarp(inout float2 current, inout float2 next, float blend)
{
    float2 currentMotion = FlipbookMotion(current);
    float2 nextMotion = FlipbookMotion(next);
    current -= currentMotion * blend;
    next += nextMotion * (1.0 - blend);
}

#endif
