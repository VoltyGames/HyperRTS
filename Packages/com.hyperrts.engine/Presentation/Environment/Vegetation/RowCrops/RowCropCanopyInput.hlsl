// HyperRTS/RowCropCanopy inputs shared by every pass: the textures, the crop values RowCropCanopy sets per draw, and
// the vertex stage that raises the ground-level mesh to the crop's top as the plants leave.
#ifndef HYPERRTS_ROW_CROP_CANOPY_INPUT_INCLUDED
#define HYPERRTS_ROW_CROP_CANOPY_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wind.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trampling/Trample.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/CraterSink.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/BuildingFootprints.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/BurntPlants.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropField.hlsl"

#define CANOPY_DOWN 0.05 // metres of crop under which it counts as flattened and casts nothing
#define CANOPY_WALL_DEPTH 0.3 // RowCropCanopyShape.WallDepth
#define CANOPY_FIRST_RISE 0.05 // share of its height the canopy is drawn from, clear of the ground under it
#define CANOPY_SHADOW_LIFT 1.0 // metres toward the sun the shadow map is read from the drawn top

TEXTURE2D(_CanopyMap);
TEXTURE2D(_CanopyNormal);
TEXTURE2D(_CanopyMask);
TEXTURE2D(_WallMap);
TEXTURE2D(_WallNormal);

// Samplers that belong to no texture, since each pass keeps only the textures it samples: the tile repeats both ways
// (URP's sampler_TrilinearRepeat), the wall only along.
SAMPLER(sampler_TrilinearRepeatU_ClampV);

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half _Cutoff;
    half _Smoothness;
    half _Translucency;
    half _HueVariation;
    half _ScreenOcclusion;
    half _WindSheen;
    float _ParallaxSteps;
    half _CanopyShade;
    half _BakedOcclusion;
    half _SunDepth;
CBUFFER_END

float _CanopyHeight; // RowCrop.height
float4 _CanopyTile;  // 1 / the tile's metres across and along the rows
float4 _CanopyFade;  // lod1 distance, metres before it the canopy rises over (Handover), the draw distance, 1 / the far fade's length
float4 _CanopyCamera; // xyz the drawing camera, which shadow passes don't have in _WorldSpaceCameraPos
float4 _CanopyPlanting; // row spacing, plant spacing

struct Attributes
{
    float4 positionOS : POSITION; // world, on the ground
    float3 normalOS : NORMAL;
    float4 canopy : TEXCOORD0;    // RowCropCanopyGeometry.Canopy
};

struct CanopyVaryings
{
    // Conservative depth output needs the position read at the centroid, unprojected.
    noperspective centroid float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float4 canopy : TEXCOORD2; // x wall tiles along the outline, y share up the wall, z 1 on walls, w metres of crop
    half stand : TEXCOORD3;    // share of the plants still standing
    nointerpolation uint field : TEXCOORD4;
    half lodged : TEXCOORD5;   // share of the crop flattened by wind and rain
};

// Share of its height the canopy has risen to: none where the plants start to leave, all where the last has gone, so
// the leaving plants stand in it rather than over holes in it.
float CanopyRisen(float3 positionWS)
{
    return Handover(_CanopyCamera.xyz, positionWS, _CanopyFade.x, _CanopyFade.y);
}

// Share of the canopy kept toward the draw distance.
float CanopyKept(float3 positionWS)
{
    return saturate((_CanopyFade.z - distance(positionWS, _CanopyCamera.xyz)) * _CanopyFade.w);
}

// Trampling bends the crop down and thins it, lodging flattens it, it grows shorter at the field's edge as the plants
// do, and ash and buildings clear it.
CanopyVaryings PlaceCanopy(Attributes input)
{
    float3 ground = input.positionOS.xyz;
    uint field = (uint)input.canopy.w;
    RowCropField sown = _Fields[field - 1u];
    TrampleSample trample = SampleTrample(ground.xz);
    float stand = (1.0 - BurntShare(ground.xz)) * saturate(2.0 - 2.0 * trample.crop);
    stand *= UnderBuilding(ground.xz) ? 0.0 : 1.0;
    float2 metres = FieldMetres(sown, ground.xz);
    Lodged lodged = Lodging(sown, metres);
    float edge = _Growth.w != 1.0 ? Edge(ground.xz, sown) : 0.0;
    float top = _CanopyHeight * FieldGrowth(sown, metres) * lerp(1.0, _Growth.w, edge) * (1.0 - trample.crop) * stand;
    top *= lerp(1.0, LODGED_HEIGHT, lodged.amount) * CanopyRisen(ground);
    float rise = lerp(-CANOPY_WALL_DEPTH, top, input.canopy.y);

    CanopyVaryings output;
    output.positionWS = SinkIntoCrater(ground + float3(0.0, rise, 0.0), ground);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = (half3)input.normalOS;
    output.canopy = float4(input.canopy.x, rise / max(top, CANOPY_DOWN), input.canopy.z, top);
    output.stand = (half)stand;
    output.field = field;
    output.lodged = (half)lodged.amount;
    return output;
}

bool OnWall(CanopyVaryings input)
{
    return input.canopy.z > 0.5;
}

// The pixel's field: the one under it, or the triangle's where the smoothed outline reaches past the crop's texels.
RowCropField CanopyField(float2 positionXZ, uint triangleField)
{
    RowCropField own = _Fields[triangleField - 1u];
    uint id = FieldAt(positionXZ);
    // Structs can't be picked by a conditional expression on every compiler.
    if (id != 0u && _Fields[id - 1u].crop == own.crop)
    {
        own = _Fields[id - 1u];
    }

    return own;
}

// Nothing before the canopy rises, where it would lie on the ground; toward the draw distance it leaves plant by plant.
void ClipCanopyFade(float3 positionWS, uint triangleField)
{
    clip(CanopyRisen(positionWS) - CANOPY_FIRST_RISE);
    RowCropField field = CanopyField(positionWS.xz, triangleField);
    int2 spot = NearestPlant(field, FieldMetres(field, positionWS.xz), _CanopyPlanting.xy);
    clip(CanopyKept(positionWS) - Channel(Hash(field.seed, spot.x, spot.y), CHANNEL_CANOPY));
}

#endif
