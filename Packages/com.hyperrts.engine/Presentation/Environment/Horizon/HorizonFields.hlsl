// The horizon's field grid as HorizonFields.cs lays it: rows of fields turned by the grid's angle, each row slid by a
// hashed share of a field, each field a crop picked by weight. Plain functions of the grid's values, so the CPU belts
// and the painted ground agree.
#ifndef HYPERRTS_HORIZON_FIELDS_INCLUDED
#define HYPERRTS_HORIZON_FIELDS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

#define HORIZON_ROW_SALT 0x52u // HorizonFields.RowSalt
#define HORIZON_CROP_SALT 0x43u
#define HORIZON_SHADE_SALT 0x53u

struct HorizonField
{
    int2 cell;    // column, row
    float2 local; // metres from the field's south-west corner, x along the rows
    float2 grid;  // the point in the grid, which runs on smoothly across fields
};

// ScatterHash.Hash(seed ^ salt, a, b).
uint HorizonHash(uint seed, uint salt, int a, int b)
{
    return Pcg3(uint3(seed ^ salt, asuint(a), asuint(b)));
}

// size is a field's length along the rows and width across them; turn the grid's (cos, sin).
HorizonField FieldAt(float2 positionXZ, float2 size, float2 turn, uint seed)
{
    HorizonField field;
    field.grid = float2(positionXZ.x * turn.x + positionXZ.y * turn.y, -positionXZ.x * turn.y + positionXZ.y * turn.x);
    int row = (int)floor(field.grid.y / size.y);
    float shifted = field.grid.x + Unit(HorizonHash(seed, HORIZON_ROW_SALT, row, 0)) * size.x;
    int column = (int)floor(shifted / size.x);
    field.cell = int2(column, row);
    field.local = float2(shifted - column * size.x, field.grid.y - row * size.y);
    return field;
}

// Metres to the field's nearest edge.
float FieldEdge(HorizonField field, float2 size)
{
    float2 toEdge = min(field.local, size - field.local);
    return min(toEdge.x, toEdge.y);
}

// The crop a field grows: the first whose running weight share passes the field's roll.
uint FieldCrop(HorizonField field, uint seed, float4 rollsLow, float4 rollsHigh)
{
    float roll = Unit(HorizonHash(seed, HORIZON_CROP_SALT, field.cell.x, field.cell.y));
    float rolls[8] = { rollsLow.x, rollsLow.y, rollsLow.z, rollsLow.w, rollsHigh.x, rollsHigh.y, rollsHigh.z, rollsHigh.w };
    uint crop = 7u;
    [unroll] for (int i = 7; i >= 0; i--)
    {
        crop = roll < rolls[i] ? (uint)i : crop;
    }

    return crop;
}

// A random value per field, for its own shade.
float FieldShade(HorizonField field, uint seed)
{
    return Unit(HorizonHash(seed, HORIZON_SHADE_SALT, field.cell.x, field.cell.y));
}

#endif
