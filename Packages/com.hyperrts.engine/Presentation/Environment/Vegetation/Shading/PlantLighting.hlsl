// Plant lighting shared by PlantShading.hlsl: wrapped sun, light through the leaf, sheen and
// point lights. Include after the shader's input file, which declares the UnityPerMaterial _Smoothness, _Translucency
// and _ScreenOcclusion, PLANT_WRAP and PLANT_TWO_SIDED.
// PLANT_VERTEX_LIGHTS lights point and spot lights per vertex instead of per pixel.
#ifndef HYPERRTS_PLANT_LIGHTING_INCLUDED
#define HYPERRTS_PLANT_LIGHTING_INCLUDED

#define PLANT_RAIN_DARKENING 0.22 // rain-soaked leaves and stems
#define PLANT_TRANSMISSION_GAIN 2.5 // light through a leaf takes its colour twice, so it reads saturated yellow-green
#define PLANT_SHADOWED_TRANSMISSION 0.3 // share that still glows where the plant's own shadow falls
#define PLANT_LEAF_F0 0.04 // a waxy leaf's reflectance head on

#ifndef PLANT_WRAP
    #error "PlantLighting.hlsl: define PLANT_WRAP (1 foliage lighting, 0 opaque solid)."
#endif

#ifndef PLANT_TWO_SIDED
    #error "PlantLighting.hlsl: define PLANT_TWO_SIDED (0 both sides lit alike, 1 side in view lit, bent skyward)."
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"

struct ForwardVaryings
{
    float4 positionCS : SV_POSITION;
    float3 uv : TEXCOORD0; // xy albedo, z height up the plant (root 0)
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tint : TEXCOORD3; // rgb per-plant shade, a canopy occlusion
    half4 ground : TEXCOORD4; // rgb ground colour over texture mean brightness, a root blend
    #if defined(PLANT_VERTEX_LIGHTS)
        half3 vertexLight : TEXCOORD5;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

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

// Per vertex for ground cover: its blades are small next to a light's reach and its cutout overdraw costs the most.
half3 PlantVertexLights(float3 positionWS, float4 positionCS, half3 normalWS)
{
    half3 lighting = half3(0.0, 0.0, 0.0);
    #if defined(_ADDITIONAL_LIGHTS)
        // The screen UV is URP's positionNDC.
        float4 ndc = positionCS * 0.5;
        float2 screenUV = (float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w) / positionCS.w;
        InputData inputData = LightLoopInput(positionWS, screenUV);
        half3 view = GetWorldSpaceNormalizeViewDir(positionWS);
        half3 normal = normalize(normalWS);
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, positionWS);
            // The side in view is only known per pixel, so both sides light alike.
            half3 facing = dot(normal, light.direction) >= 0.0 ? normal : -normal;
            half diffuse = PlantDiffuse(facing, light.direction, view, 1.0, 0.0);
            lighting += light.color * (light.distanceAttenuation * diffuse);
        LIGHT_LOOP_END
    #endif
    return lighting;
}

// Two-sided plants light the side in view, bent skyward; the rest light both sides like the whole plant.
half3 PlantNormal(half3 normalWS, bool front, half root)
{
    bool back = !front && PLANT_TWO_SIDED >= 0.5;
    half side = back ? -1.0 : 1.0;
    half skyward = max(root, PLANT_TWO_SIDED * 0.5);
    return normalize(lerp(normalize(normalWS) * side, half3(0.0, 1.0, 0.0), skyward));
}

// Unshadowed point and spot lights, so fires and explosion flashes light the plants round them.
half3 PlantAdditionalLights(ForwardVaryings input, half3 normal, half3 view, half root)
{
    half3 lighting = half3(0.0, 0.0, 0.0);
    #if defined(PLANT_VERTEX_LIGHTS)
        lighting = input.vertexLight * input.tint.a;
    #elif defined(_ADDITIONAL_LIGHTS)
        InputData inputData = LightLoopInput(input.positionWS, GetNormalizedScreenSpaceUV(input.positionCS));
        uint lightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(lightCount)
            Light light = GetAdditionalLight(lightIndex, input.positionWS);
            half diffuse = PlantDiffuse(normal, light.direction, view, input.tint.a, root);
            lighting += light.color * (light.distanceAttenuation * diffuse);
        LIGHT_LOOP_END
    #endif
    return lighting;
}

// Wrapped sun, transmission and sheen, all dimmed by the canopy occlusion; the root gets plain up-facing light.
half3 PlantLight(half3 albedo, ForwardVaryings input, half3 normal, half root)
{
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
    // Shadow and cookie only: culling attenuation isn't set for indirect draws.
    half3 direct = sun.color * sun.shadowAttenuation;
    half canopy = input.tint.a;

    // Screen AO reads dense blades as a crevice the canopy term already darkens, so each material takes only its share.
    half occlusion = canopy;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        half screen = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
        occlusion *= lerp(1.0, screen, _ScreenOcclusion);
    #endif

    half3 lit = direct * PlantDiffuse(normal, sun.direction, view, canopy, root);
    lit += PlantAdditionalLights(input, normal, view, root);
    half3 ambient = SurfaceCacheAmbient(input.positionWS, normal, view, input.positionCS.xy) * occlusion;
    half through = PlantTransmission(normal, sun.direction, canopy, root, sun.shadowAttenuation);
    half3 transmitted = sun.color * (albedo * PLANT_TRANSMISSION_GAIN * through);
    half3 sheen = direct * (PlantSheen(normal, sun.direction, view) * canopy * (1.0 - root) * PLANT_WRAP);
    return albedo * (lit + ambient + transmitted) + sheen;
}

#endif
