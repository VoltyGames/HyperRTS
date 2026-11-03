// HyperRTS/RowCropCanopy's passes. With _CANOPY_PARALLAX the forward, depth and depth-normals passes write the traced
// depth, always behind the drawn surface, so the depth test can still run early against it.
#ifndef HYPERRTS_ROW_CROP_CANOPY_PASSES_INCLUDED
#define HYPERRTS_ROW_CROP_CANOPY_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopyInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/RowCrops/RowCropCanopySurface.hlsl"

#if UNITY_REVERSED_Z
    #define CANOPY_DEPTH_SEMANTIC SV_DepthLessEqual
#else
    #define CANOPY_DEPTH_SEMANTIC SV_DepthGreaterEqual
#endif

#if defined(_CANOPY_PARALLAX)
    #define CANOPY_DEPTH_OUTPUT , out float depth : CANOPY_DEPTH_SEMANTIC
    #define CANOPY_WRITE_DEPTH(surface) depth = CanopyDepth(surface.positionWS)
#else
    #define CANOPY_DEPTH_OUTPUT
    #define CANOPY_WRITE_DEPTH(surface)
#endif

CanopyVaryings CanopyVert(Attributes input)
{
    return PlaceCanopy(input);
}

#if defined(CANOPY_FORWARD)

#define PLANT_WRAP 1.0
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantLighting.hlsl"

// Bent tops show their pale sides, so a passing gust rolls a lighter band across the field; the lulls darken.
half CanopySheen(float2 positionXZ, half share)
{
    if (_HyperRTSWindDetail < 0.5 || _WindSheen <= 0.0)
    {
        return 1.0;
    }

    return 1.0 + _WindSheen * (half)WindGust(positionXZ, WindThisFrame()) * share * share;
}

// Top-down ground colour from GroundColor, for the soil between the plants.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

// Off a map's ground colour the soil keeps the tile's own.
half3 Soil(float3 positionWS, half3 tile)
{
    float2 uv = RectUV(_HyperRTSGroundRect, positionWS.xz);
    half3 ground = SAMPLE_TEXTURE2D_LOD(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, uv, 0).rgb;
    return _HyperRTSGroundRect.z > 0.0 ? ground : tile;
}

half4 ForwardFrag(CanopyVaryings input CANOPY_DEPTH_OUTPUT) : SV_Target
{
    CanopySurface surface = CanopySurfaceAt(input);
    CANOPY_WRITE_DEPTH(surface);
    half3 crop = surface.albedo * PlantTint((half)DryPatch(surface.positionWS.xz), 0.5);
    crop *= CanopySheen(surface.positionWS.xz, surface.share) * (1.0 - PLANT_RAIN_DARKENING * _HyperRTSWetness);
    half3 albedo = lerp(Soil(surface.positionWS, surface.albedo), crop, surface.coverage);

    // The sun's shadow map is read on the drawn top, which casts it, not down in the crop it would shadow, and moved
    // toward the sun so the soft shadows' blocker search can't find the top itself; shadows of others stay put.
    PlantPixel pixel = PlantPixelAt(surface.positionWS, input.positionCS, surface.occlusion, 1.0 - surface.coverage);
    pixel.sky = surface.sky;
    pixel.shadowPositionWS = input.positionWS + _MainLightPosition.xyz * CANOPY_SHADOW_LIFT;
    #if defined(_CANOPY_PARALLAX)
        pixel.shadowFromMap = true;
    #endif
    UNITY_BRANCH
    if (!OnWall(input))
    {
        RowCropField field = CanopyField(input.positionWS.xz, input.field);
        float2 uv = CanopyUV(field, input.positionWS.xz);
        float top = max(input.canopy.w, CANOPY_DOWN);
        pixel.sunShade = CanopySelfShadow(field, surface, top, GetMainLight().direction, ddx(uv), ddy(uv));
    }

    half3 view = GetWorldSpaceNormalizeViewDir(surface.positionWS);
    half3 lights = PlantPixelLights(pixel, surface.normalWS, view);
    return half4(PlantSurfaceLight(albedo, pixel, surface.normalWS, lights), 1.0);
}

#endif

#if defined(CANOPY_PREPASSES)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

struct CanopyMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    nointerpolation uint field : TEXCOORD3;
};

CanopyVaryings ShadowVert(Attributes input)
{
    CanopyVaryings output = PlaceCanopy(input);
    output.positionCS = ShadowCasterPositionCS(output.positionWS, output.normalWS);
    return output;
}

// Toward the draw distance the canopy casts from halfway through its fade: cast plant by plant, each would stamp a
// box of shadow. Flattened crop, wheel lines and building footprints cast nothing.
half4 ShadowFrag(CanopyVaryings input) : SV_Target
{
    clip(CanopyRisen(input.positionWS) - CANOPY_FIRST_RISE);
    clip(CanopyKept(input.positionWS) - 0.5);
    UNITY_BRANCH
    if (OnWall(input))
    {
        float2 uv = float2(input.canopy.x, saturate(input.canopy.y));
        clip(SAMPLE_TEXTURE2D(_WallMap, sampler_TrilinearRepeatU_ClampV, uv).a - _Cutoff);
        return 0;
    }

    RowCropField field = CanopyField(input.positionWS.xz, input.field);
    bool cleared = OnTramline(field.tramlines, FieldMetres(field, input.positionWS.xz).x) || UnderBuilding(input.positionWS.xz);
    clip(cleared ? -1.0 : input.canopy.w - CANOPY_DOWN);
    return 0;
}

half4 DepthFrag(CanopyVaryings input CANOPY_DEPTH_OUTPUT) : SV_Target
{
    CanopySurface surface = CanopySurfaceAt(input);
    CANOPY_WRITE_DEPTH(surface);
    return 0;
}

half4 DepthNormalsFrag(CanopyVaryings input CANOPY_DEPTH_OUTPUT) : SV_Target
{
    CanopySurface surface = CanopySurfaceAt(input);
    CANOPY_WRITE_DEPTH(surface);
    return DepthNormalOutput(surface.normalWS);
}

// The canopy stands still: only the camera moves it on screen.
CanopyMotionVaryings MotionVert(Attributes input)
{
    CanopyVaryings placed = PlaceCanopy(input);
    CanopyMotionVaryings output;
    output.positionCS = placed.positionCS;
    output.positionWS = placed.positionWS;
    output.field = placed.field;
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(placed.positionWS, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(placed.positionWS, 1.0));
    return output;
}

float4 MotionFrag(CanopyMotionVaryings input) : SV_Target
{
    ClipCanopyFade(input.positionWS, input.field);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif

#endif
