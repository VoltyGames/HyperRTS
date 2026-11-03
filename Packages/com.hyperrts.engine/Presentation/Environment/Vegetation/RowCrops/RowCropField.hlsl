// The sown fields as RowCropField.cs lays them out, the field id picture and the field maths RowCrops.compute and
// HyperRTS/RowCropCanopy share, so the canopy's rows and growth meet the plants'.
#ifndef HYPERRTS_ROW_CROP_FIELD_INCLUDED
#define HYPERRTS_ROW_CROP_FIELD_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/ScatterNoise.hlsl"

#define GROWTH_SALT 0x27D4EB2Fu
#define STAGGER_SALT 0x165667B1u
#define CHANNEL_CANOPY 14u // the plant's turn to hand over to the canopy
#define EDGE_REACH 1.5 // metres probed round a point; this near the field's edge it grows shortest and leans most
#define LODGING_SALT 0x9E3779B1u
#define LODGING_CELL 30.0 // metres of the field lattice that hold at most one lodged patch
#define LODGED_BEND 0.75 // trample bend of fully lodged plants
#define LODGED_HEIGHT 0.35 // share of its height a fully lodged canopy keeps
#define HANDOVER_GRAZING 0.5 // downward share of the view under which the hand-off band reaches nearer
#define HANDOVER_WIDEN 1.5 // RowCrop.GrazingWiden: extra lengths of the band at the flattest view

struct RowCropField // RowCropField.cs, 64 bytes
{
    float2 origin;
    float2 along;
    float2 across;
    int crop;
    int seed;
    float4 tramlines; // RowCropTramlines: spacing (0 for none), gauge, half width, offset
    float4 swaths;    // RowCropSwaths: spacing (0 for none), half width, plant share, offset
};

StructuredBuffer<RowCropField> _Fields;
Texture2D<float4> _FieldIds;
float4 _FieldArea;   // x min, z min, texels per metre in x and z
float4 _FieldTexels; // field id texels across and deep
float4 _Growth;      // min and max growth scale, 1 / growth variation size, scale at the field's edge
float4 _Lodging;     // share of lattice cells with a lodged patch, smallest and largest patch across (metres)

// RowCropTramlines.Covers.
bool OnTramline(float4 tramlines, float across)
{
    if (tramlines.x <= 0.0)
    {
        return false;
    }

    float fromLine = across - tramlines.w - floor((across - tramlines.w) / tramlines.x) * tramlines.x;
    float fromCentre = min(fromLine, tramlines.x - fromLine);
    return abs(fromCentre - tramlines.y * 0.5) < tramlines.z;
}

// RowCropSwaths.Covers.
bool OnSwath(float4 swaths, float across)
{
    if (swaths.x <= 0.0)
    {
        return false;
    }

    float shifted = across + swaths.w;
    float fromCentre = abs(shifted - floor(shifted / swaths.x) * swaths.x - swaths.x * 0.5);
    return fromCentre < swaths.y;
}

uint FieldAt(float2 spot)
{
    int2 texel = (int2)floor((spot - _FieldArea.xy) * _FieldArea.zw);
    if (any(texel < 0) || any(texel >= (int2)_FieldTexels.xy))
    {
        return 0u;
    }

    float2 bytes = round(_FieldIds.Load(int3(texel, 0)).rg * 255.0);
    return (uint)bytes.x + (uint)bytes.y * 256u;
}

// Metres from the field's lattice origin: x across the rows, y along them.
float2 FieldMetres(RowCropField field, float2 positionXZ)
{
    float2 offset = positionXZ - field.origin;
    return float2(dot(offset, field.across), dot(offset, field.along));
}

// Each row slides by its own share of a spacing, so plant gaps in neighbouring rows don't line up into stripes.
float RowStagger(RowCropField field, int row)
{
    return Channel(Hash(field.seed ^ (int)STAGGER_SALT, row, 0), 0u) - 0.5;
}

// The lattice spot (row, plant) of the plant nearest a lattice position in metres, for row and plant spacing.
int2 NearestPlant(RowCropField field, float2 metres, float2 spacing)
{
    int row = (int)round(metres.x / spacing.x);
    return int2(row, (int)round(metres.y / spacing.y - RowStagger(field, row)));
}

// Scale of the field's taller and shorter stretches at a lattice position in metres.
float FieldGrowth(RowCropField field, float2 metres)
{
    return lerp(_Growth.x, _Growth.y, Noise(field.seed, GROWTH_SALT, metres * _Growth.z));
}

// Whether a spot grows the crop, in this field or another of it (a headland sown across the rows).
float Grows(float2 spot, int crop)
{
    uint id = FieldAt(spot);
    return id != 0u && _Fields[id - 1u].crop == crop ? 1.0 : 0.0;
}

// Share of four probes round the spot that fall outside its crop.
float Edge(float2 spot, RowCropField field)
{
    float inside = Grows(spot + field.across * EDGE_REACH, field.crop);
    inside += Grows(spot - field.across * EDGE_REACH, field.crop);
    inside += Grows(spot + field.along * EDGE_REACH, field.crop);
    inside += Grows(spot - field.along * EDGE_REACH, field.crop);
    return 1.0 - inside * 0.25;
}

struct Lodged
{
    float amount;     // 0 standing to 1 flattened
    float2 direction; // world XZ the crop lies toward
};

// Patches the wind and rain flattened, one at most per lattice cell, ragged at the edge and laid one way. Plants
// bend by it and the canopy drops by it, so near and far agree.
Lodged Lodging(RowCropField field, float2 metres)
{
    Lodged lodged = (Lodged)0;
    int2 cell = (int2)floor(metres / LODGING_CELL);
    uint hash = Hash(field.seed ^ (int)LODGING_SALT, cell.x, cell.y);
    if (Channel(hash, 0u) >= _Lodging.x)
    {
        return lodged;
    }

    float radius = lerp(_Lodging.y, _Lodging.z, Channel(hash, 1u)) * 0.5;
    float2 slide = float2(Channel(hash, 2u), Channel(hash, 3u)) - 0.5;
    float2 centre = (float2(cell) + 0.5) * LODGING_CELL + slide * (LODGING_CELL - 2.0 * radius);
    float ragged = 0.75 + 0.5 * Noise(field.seed, LODGING_SALT, metres * 0.4);
    lodged.amount = 1.0 - smoothstep(0.7, 1.0, length(metres - centre) / (radius * ragged));
    float heading = Channel(hash, 4u) * 6.2831853;
    lodged.direction = field.across * cos(heading) + field.along * sin(heading);
    return lodged;
}

// Share of the plants handed over to the canopy at a ground point, over the fade band before the simple mesh's far
// edge. Toward flat views the band reaches nearer, as its depth there spans few pixels and would read as a line.
float Handover(float3 camera, float3 ground, float far, float fade)
{
    float range = distance(camera, ground);
    float down = saturate((camera.y - ground.y) / max(range, 1e-3));
    float band = fade * (1.0 + HANDOVER_WIDEN * saturate(1.0 - down / HANDOVER_GRAZING));
    return saturate((range - (far - band)) / band);
}

#endif
