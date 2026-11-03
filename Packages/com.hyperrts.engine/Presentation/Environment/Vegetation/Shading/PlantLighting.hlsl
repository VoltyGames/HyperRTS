// Plant light shared by PlantShading.hlsl and HyperRTS/RowCropCanopy: wrapped sun, transmission, sheen and ambient on
// a PlantPixel. Declare _Translucency, _Smoothness, _ScreenOcclusion, _HueVariation and PLANT_WRAP first.
#ifndef HYPERRTS_PLANT_LIGHTING_INCLUDED
#define HYPERRTS_PLANT_LIGHTING_INCLUDED

#define PLANT_RAIN_DARKENING 0.22 // rain-soaked leaves and stems
#define PLANT_TRANSMISSION_GAIN 2.5 // light through a leaf takes its colour twice, so it reads saturated yellow-green
#define PLANT_SHADOWED_TRANSMISSION 0.3 // share that still glows where the plant's own shadow falls
#define PLANT_LEAF_F0 0.04 // a waxy leaf's reflectance head on

#ifndef PLANT_WRAP
    #error "PlantLighting.hlsl: define PLANT_WRAP (1 foliage lighting, 0 opaque solid)."
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"

// A lit plant pixel. canopy is its occlusion and root blends it to plain up-facing light; the shadow map is read at
// shadowPositionWS, and sunShade dims the sun where the surface shades itself. A pixel that writes a depth other than
// its drawn surface's sets shadowFromMap: the screen's shadows were resolved at the written depth, so it reads the
// cascades itself.
struct PlantPixel
{
    float3 positionWS;
    float3 shadowPositionWS;
    bool shadowFromMap;
    float4 positionCS;
    half canopy;
    half root;
    half sunShade;
    half sky; // share of the ambient light that reaches it past its neighbours
};

PlantPixel PlantPixelAt(float3 positionWS, float4 positionCS, half canopy, half root)
{
    PlantPixel pixel;
    pixel.positionWS = positionWS;
    pixel.shadowPositionWS = positionWS;
    pixel.shadowFromMap = false;
    pixel.positionCS = positionCS;
    pixel.canopy = canopy;
    pixel.root = root;
    pixel.sunShade = 1.0;
    pixel.sky = 1.0;
    return pixel;
}

// Drier plants go straw-yellow; random in [0, 1] brightens or darkens by the hue variation.
half3 PlantTint(half dry, half random)
{
    half3 straw = lerp(half3(1.0, 1.0, 1.0), half3(1.12, 1.02, 0.72), dry * _HueVariation * 4.0);
    return straw * (1.0 - _HueVariation + random * _HueVariation * 2.0);
}

// PLANT_WRAP (set by the input file): 1 lights as foliage, 0 as an opaque solid with plain Lambert light.
// Wrapped light on the facing side plus light through the leaf toward the eye; the root wraps and passes none.
half PlantDiffuse(half3 normal, half3 toLight, half3 view, half canopy, half root)
{
    half wrapping = 0.5 * (1.0 - root) * PLANT_WRAP;
    half wrap = saturate((dot(normal, toLight) + wrapping) / (1.0 + wrapping));
    half through = pow(saturate(dot(-toLight, view)), 4.0) * _Translucency * canopy * (1.0 - root) * PLANT_WRAP;
    return wrap * canopy + through;
}

// Sunlight scattered through the leaf to its side facing away from the sun, whatever the view: from above, the
// far side of a crown and the backs of blades glow instead of going flat. The root passes none.
half PlantTransmission(half3 normal, half3 toLight, half canopy, half root, half shadow)
{
    half through = saturate(-dot(normal, toLight)) * _Translucency * canopy * (1.0 - root) * PLANT_WRAP;
    return through * lerp(PLANT_SHADOWED_TRANSMISSION, 1.0, shadow);
}

// Normalised Blinn-Phong with Schlick's Fresnel: a soft waxy sheen that brightens toward grazing views.
half PlantSheen(half3 normal, half3 toLight, half3 view)
{
    half3 halfway = normalize(toLight + view);
    half power = exp2(_Smoothness * 10.0 + 1.0);
    half fresnel = PLANT_LEAF_F0 + (1.0 - PLANT_LEAF_F0) * pow(1.0 - saturate(dot(halfway, view)), 5.0);
    half lobe = (power + 8.0) / 25.13 * pow(saturate(dot(normal, halfway)), power);
    return lobe * fresnel * saturate(dot(normal, toLight));
}

// Unshadowed point and spot lights per pixel, so fires and explosion flashes light the plants round them.
half3 PlantPixelLights(PlantPixel pixel, half3 normal, half3 view)
{
    half3 lighting = half3(0.0, 0.0, 0.0);
    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData = LightLoopInput(pixel.positionWS, GetNormalizedScreenSpaceUV(pixel.positionCS));
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, pixel.positionWS);
            half diffuse = PlantDiffuse(normal, light.direction, view, pixel.canopy, pixel.root);
            lighting += light.color * (light.distanceAttenuation * diffuse);
        LIGHT_LOOP_END
    #endif
    return lighting;
}

// Where the sun's shadow is read. Screen-space shadows compile without the cascade keyword, so a pixel reading the
// cascades itself picks its cascade by hand.
float4 PlantShadowCoord(PlantPixel pixel)
{
    float4 shadowCoord = TransformWorldToShadowCoord(pixel.shadowPositionWS, pixel.shadowFromMap, float3(0.0, 0.0, 0.0));
    if (pixel.shadowFromMap && _MAIN_LIGHT_SHADOWS_SCREEN)
    {
        half cascade = ComputeCascadeIndex(pixel.shadowPositionWS);
        shadowCoord = float4(mul(_MainLightWorldToShadow[cascade], float4(pixel.shadowPositionWS, 1.0)).xyz, 1.0);
    }

    return shadowCoord;
}

// Wrapped sun, transmission and sheen, all dimmed by the canopy occlusion; the root gets plain up-facing light.
half3 PlantSurfaceLight(half3 albedo, PlantPixel pixel, half3 normal, half3 additional)
{
    half3 view = GetWorldSpaceNormalizeViewDir(pixel.positionWS);
    float4 shadowCoord = PlantShadowCoord(pixel);
    Light sun = GetMainLight(shadowCoord, pixel.shadowPositionWS, half4(1.0, 1.0, 1.0, 1.0), true, pixel.shadowFromMap);
    // Shadow and cookie only: culling attenuation isn't set for indirect draws.
    half shadow = sun.shadowAttenuation * pixel.sunShade;
    half3 direct = sun.color * shadow;
    half canopy = pixel.canopy;
    half root = pixel.root;

    // Screen AO reads dense blades as a crevice the canopy term already darkens, so each material takes only its share.
    half occlusion = canopy * pixel.sky;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        half screen = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(pixel.positionCS)).indirectAmbientOcclusion;
        occlusion *= lerp(1.0, screen, _ScreenOcclusion);
    #endif

    half3 lit = direct * PlantDiffuse(normal, sun.direction, view, canopy, root) + additional;
    half3 ambient = SurfaceCacheAmbient(pixel.positionWS, normal, view, pixel.positionCS.xy) * occlusion;
    half through = PlantTransmission(normal, sun.direction, canopy, root, shadow);
    half3 transmitted = sun.color * (albedo * PLANT_TRANSMISSION_GAIN * through);
    half3 sheen = direct * (PlantSheen(normal, sun.direction, view) * canopy * (1.0 - root) * PLANT_WRAP);
    return albedo * (lit + ambient + transmitted) + sheen;
}

#endif
