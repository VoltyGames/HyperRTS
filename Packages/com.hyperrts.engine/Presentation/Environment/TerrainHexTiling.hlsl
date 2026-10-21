// HyperRTS/Terrain hex tiling (Mikkelsen, "Practical Real-Time Hex-Tiling", JCGT 2022), so layers never visibly repeat.
// _TERRAIN_SIMPLE makes it one plain sample.
#ifndef HYPERRTS_TERRAIN_HEX_TILING_INCLUDED
#define HYPERRTS_TERRAIN_HEX_TILING_INCLUDED

// Hex grid vertices per tile, as in the paper.
#define HEX_GRID_SCALE 3.4641016
// Barycentric sharpening, and how much texel height shapes the weights.
#define HEX_FALLOFF 7.0
#define HEX_HEIGHT_CONTRAST 0.6
// Cells with less of the blend are not sampled.
#define HEX_MIN_WEIGHT 0.02
// (cos, sin) of the far sample's fixed turn, so its repeats don't line up with the near ones.
#define FAR_TURN float2(0.79863551, 0.60181502)

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

float2 RotateUV(float2 uv, float2 rotation)
{
    return float2(uv.x * rotation.x - uv.y * rotation.y, uv.x * rotation.y + uv.y * rotation.x);
}

// Hash without sine, stable for large cell indices (Hoskins, "Hash without Sine").
float3 HexHash(float2 vertex)
{
    float3 p = frac(vertex.xyx * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yzz) * p.zyx);
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

// Taller texels win where cells meet, so stones and tufts stay whole; the weights it leaves serve the other maps.
half4 SampleHexAlbedo(inout TerrainHex hex)
{
    float3 weights = 0.0;
    half4 albedo = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > HEX_MIN_WEIGHT)
        {
            half4 texel = SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, cell.uv, hex.slice,
                cell.dx, cell.dy);
            weights[i] = hex.weights[i] * lerp(1.0, texel.a, HEX_HEIGHT_CONTRAST);
            albedo += weights[i] * texel;
        }
    }

    float total = weights.x + weights.y + weights.z;
    hex.weights = weights / total;
    return albedo / total;
}

half4 SampleHex(TEXTURE2D_ARRAY_PARAM(layers, samplerLayers), TerrainHex hex)
{
    half4 value = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > 0.0)
        {
            value += hex.weights[i] * SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, cell.uv, hex.slice,
                cell.dx, cell.dy);
        }
    }

    return value;
}

// Each cell's normal turned back by its cell's rotation into layer UV space.
half3 SampleHexNormal(TerrainHex hex, half strength)
{
    half3 normal = 0.0;
    [unroll] for (uint i = 0; i < 3; i++)
    {
        TerrainHexCell cell = hex.cells[i];
        [branch] if (hex.weights[i] > 0.0)
        {
            half4 packed = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, cell.uv, hex.slice,
                cell.dx, cell.dy);
            half3 cellNormal = UnpackNormalScale(packed, strength);
            cellNormal.xy = RotateUV(cellNormal.xy, cell.rotation);
            normal += hex.weights[i] * cellNormal;
        }
    }

    return normalize(normal);
}

// A coarser, turned copy of the layer's albedo: no hex, as it only shows at a distance.
half3 SampleFarAlbedo(float2 st, float2 dx, float2 dy, float slice)
{
    float2 uv = RotateUV(st, FAR_TURN) * _FarTiling;
    float2 uvDx = RotateUV(dx, FAR_TURN) * _FarTiling;
    float2 uvDy = RotateUV(dy, FAR_TURN) * _FarTiling;
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray, sampler_AlbedoArray, uv, slice, uvDx, uvDy).rgb;
}

#endif
