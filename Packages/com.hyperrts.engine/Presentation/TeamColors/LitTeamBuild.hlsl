// HyperRTS/LitTeam piece-by-piece construction. With _BUILD on, UV3 holds each vertex's piece: x its build order (0
// first, 1 last), y 1 where the mesh has build data. Pieces _BuildProgress hasn't reached collapse to the object origin
// with one normal, so their triangles have no area in any pass and nothing is clipped.
#ifndef HYPERRTS_LIT_TEAM_BUILD_INCLUDED
#define HYPERRTS_LIT_TEAM_BUILD_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"

// Passes take UV3 as a separate input: no URP Attributes struct uses TEXCOORD3.
#ifdef _BUILD
#define BUILD_ORDER_INPUT , float2 buildOrder : TEXCOORD3
#else
#define BUILD_ORDER_INPUT
#endif

// -1 while the piece's turn hasn't come, else the metres it still has to fall. A material sets how far and over how
// much progress, so a one-piece wall panel can be lowered across the build where a hall's pieces drop in quickly.
float BuildPieceDrop(float order, float progress)
{
    // Turns end one slice early so the last piece lands exactly at 1.
    float reach = order * (1.0 - _BuildDropSlice);
    if (progress < reach)
    {
        return -1.0;
    }

    // Slabs and beds are laid where they stand; only what is lifted into place drops.
    if (order < _BuildDropFrom)
    {
        return 0.0;
    }

    float fall = 1.0 - saturate((progress - reach) / _BuildDropSlice);
    return _BuildDropHeight * fall * fall;
}

bool BuildShowsWhole(float2 buildOrder)
{
    return _BuildProgress >= 1.0 || buildOrder.y < 0.5;
}

float3 BuildPosition(float2 buildOrder, float3 positionOS)
{
    if (BuildShowsWhole(buildOrder))
    {
        return positionOS;
    }

    float drop = BuildPieceDrop(buildOrder.x, _BuildProgress);
    return drop < 0.0 ? float3(0.0, 0.0, 0.0) : positionOS - float3(0.0, drop, 0.0);
}

// Shadow bias offsets along the normal, so hidden pieces share one to stay degenerate.
void ApplyBuild(float2 buildOrder, inout float3 positionOS, inout float3 normalOS)
{
    bool hidden = !BuildShowsWhole(buildOrder) && BuildPieceDrop(buildOrder.x, _BuildProgress) < 0.0;
    positionOS = BuildPosition(buildOrder, positionOS);
    normalOS = hidden ? float3(0.0, 1.0, 0.0) : normalOS;
}

#endif
