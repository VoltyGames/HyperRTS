// The shape a scar stamp draws: a blast bowl, its thrown-earth lip and the rays of its burn, in bowl radii.
#ifndef HYPERRTS_SCAR_SHAPE_INCLUDED
#define HYPERRTS_SCAR_SHAPE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

// The floor is flat out to this share of the radius, then the wall eases up to the crest: no steeper than the
// terrain's heightmap can draw without stepping.
#define BOWL_FLOOR 0.2
// Earth slumped back off the wall in places, part way up it.
#define SLUMP_REACH 0.7
#define SLUMP_WIDTH 0.1
#define SLUMP_RISE 0.12
// The lip climbs to a sharp crest just outside the bowl, then thins with the cube of the distance as thrown earth
// does, gone by RIM_END or the stamp's edge.
#define RIM_START 0.9
#define RIM_PEAK 1.03
#define RIM_END 2.0

struct ScarPoint
{
    float2 local; // in stamp half sizes, -1 to 1
    float bowl;   // bowl radius over the stamp's
    float phase;  // per scar, 0 to 2 pi
};

// Smooth value noise from -1 to 1.
float ScarNoise(float2 p)
{
    return ValueNoise(p) * 2.0 - 1.0;
}

// About perTurn bumps round the scar, read on the unit circle so there is no seam where angles wrap.
float AroundNoise(ScarPoint p, float perTurn)
{
    float2 around = p.local * rsqrt(max(dot(p.local, p.local), 1e-8));
    return ScarNoise(around * (perTurn / TWO_PI) + p.phase * 5.3);
}

// Wobble round the edge, so hundreds of scars don't read as stamped circles.
float Ragged(ScarPoint p)
{
    return 1.0 + 0.1 * AroundNoise(p, 6.0) + 0.04 * AroundNoise(p, 17.0);
}

// Narrow streaks out from the centre, 0 to 1, where thrown earth flew furthest.
float Rays(ScarPoint p)
{
    return saturate(0.5 + 0.9 * AroundNoise(p, 36.0));
}

// Distance from the centre in bowl radii, ragged like the scorch.
float BowlReach(ScarPoint p)
{
    return length(p.local) * Ragged(p) / p.bowl;
}

// Smooth lumps from -1 to 1 over the bowl, so a floor and a lip are never machined.
float Lumps(ScarPoint p, float perRadius)
{
    float2 at = p.local / p.bowl * perRadius + p.phase * 7.0;
    return 0.7 * ScarNoise(at) + 0.3 * ScarNoise(at * 2.1 + 3.7);
}

// The bowl's depth as a share of the full depth, lumpy: about 1 at the centre and 0 at the edge.
float BowlDepth(ScarPoint p)
{
    float reach = BowlReach(p);
    float bowl = 1.0 - smoothstep(BOWL_FLOOR, 1.0, reach);
    float slump = exp(-Sq((reach - SLUMP_REACH) / SLUMP_WIDTH)) * saturate(0.5 + Lumps(p, 3.0));
    bowl = saturate(bowl - SLUMP_RISE * slump);
    return bowl * (1.0 + 0.15 * Lumps(p, 4.0) + 0.08 * Lumps(p, 11.0));
}

// The lip's height as a share of the rim height: piled higher on some sides and out along the rays.
float LipHeight(ScarPoint p)
{
    float reach = BowlReach(p);
    float end = min(RIM_END, 1.0 / p.bowl);
    float thin = RIM_PEAK / max(reach, RIM_PEAK);
    float fade = 1.0 - smoothstep(lerp(RIM_PEAK, end, 0.6), end, reach);
    float lip = smoothstep(RIM_START, RIM_PEAK, reach) * thin * thin * thin * fade;
    float pile = saturate(0.7 + 0.3 * Lumps(p, 2.5) + 0.2 * Lumps(p, 9.0));
    return lip * pile * lerp(1.0, 0.4 + 0.6 * Rays(p), smoothstep(RIM_PEAK, 1.4, reach));
}

// The burn's share, 0 to 1, streaked into rays toward its ragged edge.
float ScorchShare(ScarPoint p)
{
    float reach = length(p.local) * Ragged(p);
    return (1.0 - smoothstep(0.55, 1.0, reach)) * lerp(1.0, Rays(p), smoothstep(0.35, 0.8, reach));
}

#endif
