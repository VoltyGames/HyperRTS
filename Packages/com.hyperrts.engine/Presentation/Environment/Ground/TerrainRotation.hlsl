// HyperRTS/Terrain's UV turn, shared by the hex tiling and the layer parameters.
#ifndef HYPERRTS_TERRAIN_ROTATION_INCLUDED
#define HYPERRTS_TERRAIN_ROTATION_INCLUDED

// Turns uv clockwise by rotation, the turn's (cos, sin).
float2 RotateUV(float2 uv, float2 rotation)
{
    return float2(uv.x * rotation.x - uv.y * rotation.y, uv.x * rotation.y + uv.y * rotation.x);
}

#endif
