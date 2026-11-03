// HyperRTS/RowCropCanopy's surface: the canopy tile laid on each field's rows, the parallax march down through the crop
// (_CANOPY_PARALLAX) and toward the sun (_CANOPY_SELF_SHADOW), and the walls. Include after RowCropCanopyInput.hlsl.
#ifndef HYPERRTS_ROW_CROP_CANOPY_SURFACE_INCLUDED
#define HYPERRTS_ROW_CROP_CANOPY_SURFACE_INCLUDED

#define CANOPY_FEWEST_STEPS 6.0 // parallax steps looking straight down; _ParallaxSteps at grazing views
#define CANOPY_GRAZING 0.25 // downward share of the view ray under which parallax fades out, as its run grows long
#define CANOPY_SHADOW_STEPS 5
#define CANOPY_SHADOW_REACH 1.5 // metres the march toward the sun runs at most
#define CANOPY_SHADOW_SOFTNESS 8.0 // how fast the sun dims as the ray sinks under the crop
#define CANOPY_SIDE_HEIGHT 0.6 // height share of the stalks a slanted view meets in place of the soil
#define CANOPY_SIDE_START 0.7 // downward share of the view under which the side view comes in
#define CANOPY_SIDE_RANGE 0.45 // and over which it takes over
#define CANOPY_SIDE_ROW 0.7 // height share up the wall texture the side view's colour is read at
#define CANOPY_SIDE_LEAF 0.75 // height share of the heads a flat view meets
#define CANOPY_SIDE_DETAIL 0.5 // how much the tile's height still marks the side view, so it doesn't read flat
#define LODGED_LIGHT 0.2 // a flattened crop shows the pale undersides of its stalks

struct CanopySurface
{
    float3 positionWS; // where the view ray meets the crop
    float3 groundWS;   // the ground under the drawn surface
    float2 uv;         // on the canopy tile
    half share;        // height up the crop, 0 at the ground
    half leafHeight;   // the tile's height share where the view lands, which the plants' shading follows
    half3 normalWS;
    half3 albedo;
    half coverage;     // 1 where a standing plant is hit, 0 on bare soil
    half occlusion;    // the plants' shade below their heads
    half sky;          // the baked occlusion from the neighbours, on the ambient light alone
};

float2 CanopyUV(RowCropField field, float2 positionXZ)
{
    return FieldMetres(field, positionXZ) * _CanopyTile.xy;
}

// The crop's height share at a point: the tile's height, none on the wheel lines.
float CanopyHeight(RowCropField field, float2 positionXZ, float2 dx, float2 dy)
{
    float2 metres = FieldMetres(field, positionXZ);
    float height = SAMPLE_TEXTURE2D_GRAD(_CanopyMap, sampler_TrilinearRepeat, metres * _CanopyTile.xy, dx, dy).a;
    return OnTramline(field.tramlines, metres.x) ? 0.0 : height;
}

// Marches the view ray from the drawn top down through the crop to where it meets the tile's height. Toward
// grazing views the depth marched shrinks to nothing, so the surface settles flat on the top.
float3 TraceCanopy(RowCropField field, float3 topWS, float top, float2 dx, float2 dy, out half share)
{
    share = 1.0;
    float3 hit = topWS;
#if defined(_CANOPY_PARALLAX)
    float3 ray = normalize(topWS - _WorldSpaceCameraPos.xyz);
    float down = max(-ray.y, 1e-3);
    float depth = top * saturate((down - CANOPY_GRAZING) / CANOPY_GRAZING);
    UNITY_BRANCH
    if (depth > CANOPY_DOWN)
    {
        float steps = round(lerp(_ParallaxSteps, CANOPY_FEWEST_STEPS, down));
        float3 run = ray * (depth / down);
        float reached = 0.0;
        float above = 1.0 - CanopyHeight(field, topWS.xz, dx, dy);
        [loop]
        for (float step = 1.0; step <= steps && above > 0.0; step += 1.0)
        {
            float along = step / steps;
            float below = CanopyHeight(field, (topWS + run * along).xz, dx, dy) - (1.0 - along);
            // Between the last step above the crop and the first under it, where the two lines cross.
            reached = below >= 0.0 ? along - below / (above + below) / steps : along;
            above = below >= 0.0 ? 0.0 : -below;
        }

        hit = topWS + run * reached;
        share = (half)(1.0 - reached * depth / top);
    }
#endif
    return hit;
}

// Sun left in the crop: it thins toward the roots, faster the lower the sun, as plants shade the plants behind them.
// With _CANOPY_SELF_SHADOW a short march toward the sun adds the shadows of the rows themselves.
half CanopySelfShadow(RowCropField field, CanopySurface surface, float top, half3 toLight, float2 dx, float2 dy)
{
    half lit = (half)exp2(-_SunDepth * (1.0 - surface.leafHeight) / max(toLight.y, 0.2));
#if defined(_CANOPY_PARALLAX) && defined(_CANOPY_SELF_SHADOW)
    float reach = min((1.0 - surface.share) * top / max(toLight.y, 0.05), CANOPY_SHADOW_REACH);
    [unroll]
    for (int step = 1; step <= CANOPY_SHADOW_STEPS; step++)
    {
        float3 probe = surface.positionWS + toLight * (reach * step / CANOPY_SHADOW_STEPS);
        float rayShare = (probe.y - surface.groundWS.y) / top;
        float crop = CanopyHeight(field, probe.xz, dx, dy);
        lit = min(lit, (half)saturate(1.0 - (crop - rayShare) * CANOPY_SHADOW_SOFTNESS));
    }
#endif
    return lit;
}

// Light left under the heads, as HyperRTS/RowCrop shades its plants: it falls off fast below them.
half CanopyLight(half share)
{
    return lerp(1.0 - _CanopyShade, 1.0, share * share);
}

// Normal map X across the rows, Y along them, round the drawn surface's normal.
half3 CanopyNormal(RowCropField field, half3 surfaceNormal, half3 normalTS)
{
    half3 across = normalize(half3(field.across.x, 0.0, field.across.y));
    half3 along = normalize(half3(field.along.x, 0.0, field.along.y));
    return normalize(across * normalTS.x + along * normalTS.y + surfaceNormal * normalTS.z);
}

// Share of the soil a slanted view loses behind the rows it crosses on its way down through the crop: the tile is
// seen from straight above, where the soil between the rows shows.
half CanopySlant(float3 positionWS, float top)
{
    float3 ray = normalize(positionWS - _CanopyCamera.xyz);
    float across = sqrt(saturate(1.0 - ray.y * ray.y)) / max(-ray.y, 0.05);
    return (half)(1.0 - exp(-top * across / _CanopyPlanting.x));
}

// Seen flat, the view meets the sides of the heads rather than the top-down tile with the soil between its rows.
half CanopySideView(float3 positionWS)
{
    float3 ray = normalize(positionWS - _CanopyCamera.xyz);
    return (half)saturate((CANOPY_SIDE_START + ray.y) / CANOPY_SIDE_RANGE);
}

CanopySurface TopSurface(CanopyVaryings input)
{
    RowCropField field = CanopyField(input.positionWS.xz, input.field);
    float top = max(input.canopy.w, CANOPY_DOWN);
    float2 uv = CanopyUV(field, input.positionWS.xz);
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    CanopySurface surface;
    surface.positionWS = TraceCanopy(field, input.positionWS, top, dx, dy, surface.share);
    surface.groundWS = input.positionWS - float3(0.0, top, 0.0);
    surface.uv = CanopyUV(field, surface.positionWS.xz);
    half4 colour = SAMPLE_TEXTURE2D_GRAD(_CanopyMap, sampler_TrilinearRepeat, surface.uv, dx, dy);
    half4 mask = SAMPLE_TEXTURE2D_GRAD(_CanopyMask, sampler_TrilinearRepeat, surface.uv, dx, dy);
    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_CanopyNormal, sampler_TrilinearRepeat, surface.uv, dx, dy));
    float2 metres = FieldMetres(field, surface.positionWS.xz);
    bool cleared = OnTramline(field.tramlines, metres.x) || UnderBuilding(surface.positionWS.xz);
    half slanted = CanopySlant(surface.positionWS, top);
    half side = CanopySideView(surface.positionWS);
    float2 sideUV = float2(metres.y * _CanopyTile.y, CANOPY_SIDE_ROW);
    half3 sideColour = SAMPLE_TEXTURE2D(_WallMap, sampler_TrilinearRepeatU_ClampV, sideUV).rgb;
    sideColour *= 1.0 + CANOPY_SIDE_DETAIL * (colour.a - 0.5);
    surface.coverage = cleared ? 0.0 : lerp(lerp(mask.g, 1.0, slanted), 1.0, side) * input.stand;
    surface.leafHeight = lerp(lerp(colour.a, max(colour.a, CANOPY_SIDE_HEIGHT), slanted), CANOPY_SIDE_LEAF, side);
    surface.occlusion = CanopyLight(surface.leafHeight);
    surface.sky = lerp(lerp(1.0, mask.r, _BakedOcclusion), 1.0, side);
    surface.albedo = lerp(colour.rgb, sideColour, side) * _BaseColor.rgb * (1.0 + LODGED_LIGHT * input.lodged);
    surface.normalWS = normalize(lerp(CanopyNormal(field, normalize(input.normalWS), normalTS), input.normalWS, 1.0 - surface.coverage));
    return surface;
}

// The field's edge seen from the side, cut out at its ragged top.
CanopySurface WallSurface(CanopyVaryings input)
{
    float2 uv = float2(input.canopy.x, saturate(input.canopy.y));
    half4 colour = SAMPLE_TEXTURE2D(_WallMap, sampler_TrilinearRepeatU_ClampV, uv);
    clip(colour.a - _Cutoff);
    half3 outward = normalize(input.normalWS);
    half3 along = half3(-outward.z, 0.0, outward.x);
    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_WallNormal, sampler_TrilinearRepeatU_ClampV, uv));

    CanopySurface surface;
    surface.positionWS = input.positionWS;
    surface.groundWS = input.positionWS;
    surface.uv = uv;
    surface.share = (half)uv.y;
    surface.normalWS = normalize(along * normalTS.x + half3(0.0, 1.0, 0.0) * normalTS.y + outward * normalTS.z);
    surface.albedo = colour.rgb * _BaseColor.rgb;
    surface.coverage = 1.0;
    surface.leafHeight = (half)uv.y;
    surface.occlusion = CanopyLight(uv.y);
    surface.sky = 1.0;
    return surface;
}

CanopySurface CanopySurfaceAt(CanopyVaryings input)
{
    ClipCanopyFade(input.positionWS, input.field);
    CanopySurface surface;
    UNITY_BRANCH
    if (OnWall(input))
    {
        surface = WallSurface(input);
    }
    else
    {
        surface = TopSurface(input);
    }

    return surface;
}

// The traced point's depth, never nearer than the drawn surface's, so early depth testing holds.
float CanopyDepth(float3 positionWS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    return positionCS.z / positionCS.w;
}

#endif
