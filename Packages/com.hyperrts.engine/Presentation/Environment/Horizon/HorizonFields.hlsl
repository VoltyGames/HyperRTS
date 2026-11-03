// HyperRTS/HorizonGround's fields: the field under a point as HorizonFields lays them out, and the layer colours
// they show near and far. The including shader declares the field and variation properties in its
// UnityPerMaterial buffer before including this.
#ifndef HYPERRTS_HORIZON_FIELDS_INCLUDED
#define HYPERRTS_HORIZON_FIELDS_INCLUDED

TEXTURE2D_ARRAY(_AlbedoArray);
SAMPLER(sampler_AlbedoArray);
TEXTURE2D(_LayerParams);
TEXTURE2D(_Fields);

// Where a pixel spans this share of a field's width, fields have fully faded to their mean.
#define HORIZON_MEAN_FOOTPRINT 0.3
#define HORIZON_MEAN_MIP 16.0

struct HorizonField
{
    float4 field; // crop layer, shade, start and end along the cell (m)
    float4 ends;  // margin at the field's start and end (m), block shade
    float4 row;   // the row's slide, start and end across the grid row (m)
    float4 sides; // margin along the row's south and north edges (m)
    float2 local; // metres along the cell and across the grid row
    float2 grid;  // the point in the grid, which runs on smoothly across fields
};

// The field under a point, as HorizonFields.FieldAt finds it: the grid row and its part, then the cell its slide
// puts the point in and the cell's part.
HorizonField FieldAt(float2 positionXZ)
{
    float2 size = _FieldGrid.xy;
    float2 turn = _FieldGrid.zw;
    HorizonField field;
    field.grid = float2(positionXZ.x * turn.x + positionXZ.y * turn.y, -positionXZ.x * turn.y + positionXZ.y * turn.x);
    int gridRow = (int)floor(field.grid.y / size.y);
    field.local.y = field.grid.y - gridRow * size.y;
    uint texelRow = 2u * (uint)(gridRow - (int)_FieldCells.y) + 1u;
    texelRow += field.local.y >= LOAD_TEXTURE2D(_Fields, uint2(0, texelRow)).b ? 1u : 0u;
    field.row = LOAD_TEXTURE2D(_Fields, uint2(0, texelRow));
    field.sides = LOAD_TEXTURE2D(_Fields, uint2(1, texelRow));
    float along = field.grid.x + field.row.r;
    int column = (int)floor(along / size.x);
    field.local.x = along - column * size.x;
    uint texel = 2u + 4u * (uint)(column - (int)_FieldCells.x);
    texel += field.local.x >= LOAD_TEXTURE2D(_Fields, uint2(texel, texelRow)).a ? 2u : 0u;
    field.field = LOAD_TEXTURE2D(_Fields, uint2(texel, texelRow));
    field.ends = LOAD_TEXTURE2D(_Fields, uint2(texel + 1u, texelRow));
    return field;
}

// Metres inside the field's margins and tracks; negative within them.
float MarginDistance(HorizonField field)
{
    float2 along = float2(field.local.x - field.field.b - field.ends.x, field.field.a - field.local.x - field.ends.y);
    float2 across = float2(field.local.y - field.row.g - field.sides.x, field.row.b - field.local.y - field.sides.y);
    return min(min(along.x, along.y), min(across.x, across.y));
}

// HyperRTS/Terrain's RotateUV: (cos, sin) of a clockwise turn.
float2 Turn(float2 uv, float2 rotation)
{
    return float2(uv.x * rotation.x - uv.y * rotation.y, uv.x * rotation.y + uv.y * rotation.x);
}

// A layer's albedo and tint as the terrain tiles it, row layers laid along the fields' rows.
half3 LayerAlbedo(float layer, float2 positionXZ)
{
    float4 mapping = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 0));
    half3 tint = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1)).rgb;
    float rowLayer = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 3)).w == 0.0;
    float2 rowTurn = float2(_FieldGrid.z, -_FieldGrid.w);
    float2 rotation = rowLayer > 0.0 ? Turn(mapping.xy, rowTurn) : mapping.xy;
    float2 uv = Turn(positionXZ, rotation) * mapping.zw;
    return SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, uv, layer).rgb * tint;
}

// A layer's mean colour, from its smallest mip.
half3 LayerMean(float layer)
{
    half3 tint = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1)).rgb;
    return SAMPLE_TEXTURE2D_ARRAY_LOD(_AlbedoArray, sampler_AlbedoArray, float2(0.5, 0.5), layer,
        HORIZON_MEAN_MIP).rgb * tint;
}

// The weighted mean of the crops and margins, what a field-sized pixel sees.
half3 FarmlandMean()
{
    half3 crops = 0.0;
    for (uint i = 0; i < (uint)_FieldCells.z; i++)
    {
        float2 crop = LOAD_TEXTURE2D(_Fields, uint2(i, 0)).rg;
        crops += LayerMean(crop.x) * crop.y;
    }

    return lerp(crops, LayerMean(_FieldMargin.x), _FieldMargin.y);
}

half3 HorizonAlbedo(float2 positionXZ)
{
    float2 size = _FieldGrid.xy;
    HorizonField field = FieldAt(positionXZ);
    float2 footprint = fwidth(field.grid);
    float blur = max(footprint.x, footprint.y);
    half far = saturate(blur / (min(size.x, size.y) * HORIZON_MEAN_FOOTPRINT));
    half3 albedo;
    [branch] if (far < 1.0)
    {
        half marginWeight = 1.0 - smoothstep(-blur, blur, MarginDistance(field));
        half3 crop = LayerAlbedo(field.field.r, positionXZ);
        albedo = lerp(crop, LayerAlbedo(_FieldMargin.x, positionXZ), marginWeight);
        albedo *= 1.0 + (field.field.g - 0.5) * 2.0 * _Variation;
        albedo *= 1.0 + (field.ends.z - 0.5) * 2.0 * _BlockVariation;
        [branch] if (far > 0.0)
        {
            albedo = lerp(albedo, FarmlandMean(), far);
        }
    }
    else
    {
        albedo = FarmlandMean();
    }

    return albedo;
}

#endif
