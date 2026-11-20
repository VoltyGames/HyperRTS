// HyperRTS/Terrain hex grid (Mikkelsen, "Practical Real-Time Hex-Tiling", JCGT 2022): the cells and weights any tiled
// texture blends by, so it never visibly repeats. _TERRAIN_SIMPLE makes it one plain sample.
#ifndef HYPERRTS_HEX_GRID_INCLUDED
#define HYPERRTS_HEX_GRID_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Hash.hlsl"

// Turns uv clockwise by rotation, the turn's (cos, sin); hex cells and the terrain layers share it.
float2 RotateUV(float2 uv, float2 rotation)
{
    return float2(uv.x * rotation.x - uv.y * rotation.y, uv.x * rotation.y + uv.y * rotation.x);
}

// Hex grid vertices per tile, as in the paper.
#define HEX_GRID_SCALE 3.4641016
// Barycentric sharpening, and how much texel height shapes the weights.
#define HEX_FALLOFF 7.0
#define HEX_HEIGHT_CONTRAST 0.6
// Cells with less of the blend are not sampled.
#define HEX_MIN_WEIGHT 0.02

struct TerrainHexCell
{
    float2 uv;
    float2 dx;
    float2 dy;
    float2 rotation;   // (cos, sin) turning the cell's texture back into layer UV space
};

struct TerrainHex
{
    TerrainHexCell cells[3];
    float3 weights;
    float slice;
};

struct TerrainHexGrid
{
    float2 vertices[3];
    float3 barycentrics;
};

// Three independent [0, 1) values per grid vertex.
float3 HexHash(float2 vertex)
{
    uint hash = Pcg3(asuint(int3((int2)vertex, 0)));
    return float3(Channel(hash, 0u), Channel(hash, 1u), Channel(hash, 2u));
}

// The three triangle-grid vertices around st, in the skewed grid, and their barycentric weights.
TerrainHexGrid GetHexGrid(float2 st)
{
    float2 grid = st * HEX_GRID_SCALE;
    float2 skewed = float2(grid.x - 0.57735027 * grid.y, 1.15470054 * grid.y);
    float2 base = floor(skewed);
    float3 corner = float3(frac(skewed), 0.0);
    corner.z = 1.0 - corner.x - corner.y;
    float upper = step(0.0, -corner.z);
    float side = 2.0 * upper - 1.0;
    TerrainHexGrid hexGrid;
    hexGrid.barycentrics = float3(-corner.z * side, upper - corner.y * side, upper - corner.x * side);
    hexGrid.vertices[0] = base + float2(upper, upper);
    hexGrid.vertices[1] = base + float2(upper, 1.0 - upper);
    hexGrid.vertices[2] = base + float2(1.0 - upper, upper);
    return hexGrid;
}

// hexParams: x rotation strength (fraction of a full turn), y 1 if the offset may go any way, else it slides only
// along zw (the layer's lines in UV, unit length).
TerrainHexCell GetHexCell(float2 st, float2 dx, float2 dy, float2 vertex, float4 hexParams)
{
    float3 random = HexHash(vertex);
    float angle = (random.z - 0.5) * TWO_PI * hexParams.x;
    TerrainHexCell cell;
    sincos(angle, cell.rotation.y, cell.rotation.x);
    float2 centre = float2(vertex.x + 0.5 * vertex.y, 0.8660254 * vertex.y) / HEX_GRID_SCALE;
    float2 inverse = float2(cell.rotation.x, -cell.rotation.y);
    float2 offset = lerp(random.x * hexParams.zw, random.xy, hexParams.y);
    cell.uv = RotateUV(st - centre, inverse) + centre + offset;
    cell.dx = RotateUV(dx, inverse);
    cell.dy = RotateUV(dy, inverse);
    return cell;
}

TerrainHex GetSimpleHex(float2 st, float2 dx, float2 dy, float slice)
{
    TerrainHex hex = (TerrainHex)0;
    hex.cells[0].uv = st;
    hex.cells[0].dx = dx;
    hex.cells[0].dy = dy;
    hex.cells[0].rotation = float2(1.0, 0.0);
    hex.weights = float3(1.0, 0.0, 0.0);
    hex.slice = slice;
    return hex;
}

TerrainHex GetHex(float2 st, float2 dx, float2 dy, float slice, float4 hexParams)
{
    #ifdef _TERRAIN_SIMPLE
        return GetSimpleHex(st, dx, dy, slice);
    #else
        TerrainHexGrid grid = GetHexGrid(st);
        TerrainHex hex;
        [unroll] for (uint i = 0; i < 3; i++)
        {
            hex.cells[i] = GetHexCell(st, dx, dy, grid.vertices[i], hexParams);
        }

        float3 falloff = pow(max(grid.barycentrics, 0.0), HEX_FALLOFF);
        hex.weights = falloff / (falloff.x + falloff.y + falloff.z);
        hex.slice = slice;
        return hex;
    #endif
}

#endif
