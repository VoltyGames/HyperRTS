// Shading shared by HyperRTS/Foliage and HyperRTS/RowCrop. Include after the shader's input file, which declares
// _BaseMap, the UnityPerMaterial shading properties (_ScreenOcclusion among them) and PLANT_TWO_SIDED; its passes
// place the vertices.
// PLANT_VERTEX_LIGHTS lights point and spot lights per vertex instead of per pixel.
#ifndef HYPERRTS_PLANT_SHADING_INCLUDED
#define HYPERRTS_PLANT_SHADING_INCLUDED

#define PLANT_RAIN_DARKENING 0.22 // rain-soaked leaves and stems

#ifndef PLANT_TWO_SIDED
    #error "PlantShading.hlsl: define PLANT_TWO_SIDED (0 both sides lit alike, 1 side in view lit, bent skyward)."
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wetness.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Top-down ground colour from GroundColor. Rect: x min, z min, 1 / width, 1 / depth, zero outside a map.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

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

struct CutoutVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Ground colour over the texture's mean brightness, so the root keeps the texture's detail.
half4 RootGround(float3 origin)
{
    float2 uv = RectUV(_HyperRTSGroundRect, origin.xz);
    half3 ground = SAMPLE_TEXTURE2D_LOD(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, uv, 0).rgb;
    half mean = Luminance(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 16).rgb);
    half weight = _HyperRTSGroundRect.z > 0.0 ? _GroundBlend : 0.0;
    return half4(ground / max(mean, 0.02), weight);
}

// Drier plants go straw-yellow; random in [0, 1] brightens or darkens by the hue variation.
half3 PlantTint(half dry, half random)
{
    half3 straw = lerp(half3(1.0, 1.0, 1.0), half3(1.12, 1.02, 0.72), dry * _HueVariation * 4.0);
    return straw * (1.0 - _HueVariation + random * _HueVariation * 2.0);
}

void ClipCoverage(float2 uv)
{
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
}

// LOD crossfade dither. positionCS is the fragment's SV_POSITION.
void PlantCrossFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(positionCS);
#endif
}

// Wrapped light on the facing side plus light through the leaf toward the eye; the root wraps and passes none.
half PlantDiffuse(half3 normal, half3 toLight, half3 view, half canopy, half root)
{
    half wrapping = 0.5 * (1.0 - root);
    half wrap = saturate((dot(normal, toLight) + wrapping) / (1.0 + wrapping));
    half through = pow(saturate(dot(-toLight, view)), 4.0) * _Translucency * canopy * (1.0 - root);
    return wrap * canopy + through;
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

ForwardVaryings PlantForwardVaryings(float3 positionWS, float3 origin, float2 uv, float height, half3 normalWS, half4 tint)
{
    ForwardVaryings output = (ForwardVaryings)0;
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = float3(TRANSFORM_TEX(uv, _BaseMap), height);
    output.normalWS = normalWS;
    output.tint = tint;
    output.ground = RootGround(origin);
    #if defined(PLANT_VERTEX_LIGHTS)
        output.vertexLight = PlantVertexLights(positionWS, output.positionCS, normalWS);
    #endif
    return output;
}

CutoutVaryings PlantCutoutVaryings(float4 positionCS, float2 uv, half3 normalWS)
{
    CutoutVaryings output = (CutoutVaryings)0;
    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(uv, _BaseMap);
    output.normalWS = normalWS;
    return output;
}

// Last frame's position comes from last frame's wind.
MotionVaryings PlantMotionVaryings(float3 now, float3 last, float2 uv)
{
    MotionVaryings output = (MotionVaryings)0;
    output.positionCS = TransformWorldToHClip(now);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(now, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(last, 1.0));
    output.uv = TRANSFORM_TEX(uv, _BaseMap);
    return output;
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

// Wrapped sun, translucency and glint, all dimmed by the canopy occlusion; the root gets plain up-facing light.
half3 PlantLight(half3 albedo, ForwardVaryings input, half3 normal, half root)
{
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
    // Shadow and cookie only: culling attenuation isn't set for indirect draws.
    half3 direct = sun.color * sun.shadowAttenuation;
    half canopy = input.tint.a;
    half3 halfway = normalize(sun.direction + view);
    half glint = pow(saturate(dot(normal, halfway)), exp2(_Smoothness * 10.0 + 1.0)) * _Smoothness;

    // Screen AO reads a dense canopy of thin blades as a deep crevice, so materials whose canopy occlusion already
    // shades them take less of it.
    half occlusion = canopy;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        half screen = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
        occlusion *= lerp(1.0, screen, _ScreenOcclusion);
    #endif

    half3 lit = direct * PlantDiffuse(normal, sun.direction, view, canopy, root);
    lit += PlantAdditionalLights(input, normal, view, root);
    half3 ambient = SurfaceCacheAmbient(input.positionWS, normal, view, input.positionCS.xy) * occlusion;
    return albedo * (lit + ambient) + glint * canopy * direct;
}

half4 ForwardFrag(ForwardVaryings input, bool front : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
    half4 albedo = texel * _BaseColor;
    clip(albedo.a - _Cutoff);
    PlantCrossFade(input.positionCS);
    albedo.rgb *= input.tint.rgb * (1.0 - PLANT_RAIN_DARKENING * _HyperRTSWetness);

    // The root fades into the ground's colour.
    half root = input.ground.a * (1.0 - smoothstep(0.0, _GroundBlendHeight, input.uv.z));
    albedo.rgb = lerp(albedo.rgb, input.ground.rgb * Luminance(texel.rgb), root);

    half3 normal = PlantNormal(input.normalWS, front, root);
    half3 color = PlantLight(albedo.rgb, input, normal, root);
    return half4(color, 1.0);
}

half4 ShadowFrag(CutoutVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipCoverage(input.uv);
    PlantCrossFade(input.positionCS);
    return 0;
}

half4 DepthFrag(CutoutVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipCoverage(input.uv);
    PlantCrossFade(input.positionCS);
    return input.positionCS.z;
}

half4 DepthNormalsFrag(CutoutVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipCoverage(input.uv);
    PlantCrossFade(input.positionCS);
    return DepthNormalOutput(normalize(input.normalWS));
}

float4 MotionFrag(MotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipCoverage(input.uv);
    PlantCrossFade(input.positionCS);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif
