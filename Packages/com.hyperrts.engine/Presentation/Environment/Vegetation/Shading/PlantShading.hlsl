// Shading shared by HyperRTS/Foliage, HyperRTS/RowCrop and HyperRTS/HorizonBelt, lit by PlantLighting.hlsl. Include
// after the shader's input file, which declares _BaseMap, the UnityPerMaterial shading properties and
// PLANT_TWO_SIDED; its pass file places the vertices.
// PLANT_VERTEX_LIGHTS lights point and spot lights per vertex instead of per pixel.
#ifndef HYPERRTS_PLANT_SHADING_INCLUDED
#define HYPERRTS_PLANT_SHADING_INCLUDED

#ifndef PLANT_TWO_SIDED
    #error "PlantShading.hlsl: define PLANT_TWO_SIDED (0 both sides lit alike, 1 side in view lit, bent skyward)."
#endif

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/VegetationMath.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"
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

// Ground colour over the texture's mean brightness, so the root keeps the texture's detail.
half4 RootGround(float3 origin)
{
    float2 uv = RectUV(_HyperRTSGroundRect, origin.xz);
    half3 ground = SAMPLE_TEXTURE2D_LOD(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, uv, 0).rgb;
    half mean = Luminance(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 16).rgb);
    half weight = _HyperRTSGroundRect.z > 0.0 ? _GroundBlend : 0.0;
    return half4(ground / max(mean, 0.02), weight);
}

// LOD crossfade dither. positionCS is the fragment's SV_POSITION.
void PlantCrossFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(positionCS);
#endif
}

// A negative _MipBias keeps thin blades and stalks sharp for temporal anti-aliasing.
half4 SamplePlant(float2 uv)
{
    return SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv, _MipBias);
}

// DisplacedPasses.hlsl's alpha cut for every plant prepass.
void ClipDisplaced(float2 uv, float4 positionCS)
{
    clip(SamplePlant(uv).a * _BaseColor.a - _Cutoff);
    PlantCrossFade(positionCS);
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

// Two-sided plants light the side in view, bent skyward; the rest light both sides like the whole plant.
half3 PlantNormal(half3 normalWS, bool front, half root)
{
    bool back = !front && PLANT_TWO_SIDED >= 0.5;
    half side = back ? -1.0 : 1.0;
    half skyward = max(root, PLANT_TWO_SIDED * 0.5);
    return normalize(lerp(normalize(normalWS) * side, half3(0.0, 1.0, 0.0), skyward));
}

// Point and spot lights per vertex or per pixel, then PlantSurfaceLight.
half3 PlantLight(half3 albedo, ForwardVaryings input, half3 normal, half root)
{
    PlantPixel pixel = PlantPixelAt(input.positionWS, input.positionCS, input.tint.a, root);
    #if defined(PLANT_VERTEX_LIGHTS)
        half3 additional = input.vertexLight * input.tint.a;
    #else
        half3 additional = PlantPixelLights(pixel, normal, GetWorldSpaceNormalizeViewDir(input.positionWS));
    #endif
    return PlantSurfaceLight(albedo, pixel, normal, additional);
}

half4 ForwardFrag(ForwardVaryings input, bool front : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 texel = SamplePlant(input.uv.xy);
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

#endif
