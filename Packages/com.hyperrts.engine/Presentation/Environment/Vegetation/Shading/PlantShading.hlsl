// Shading shared by HyperRTS/Foliage and HyperRTS/RowCrop: PlantLighting.hlsl's light on a textured plant whose root
// fades into the ground. Include after the shader's input file, which declares _BaseMap, the UnityPerMaterial shading
// properties, PLANT_WRAP and PLANT_TWO_SIDED; its pass file places the vertices.
#ifndef HYPERRTS_PLANT_SHADING_INCLUDED
#define HYPERRTS_PLANT_SHADING_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Top-down ground colour from GroundColor. Rect: x min, z min, 1 / width, 1 / depth, zero outside a map.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

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
