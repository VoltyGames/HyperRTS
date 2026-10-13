// HyperRTS/RowCrop passes, lit like HyperRTS/Foliage (wrap light, light shining through from behind, the ground's
// colour at the roots). Every pass places vertices with RowCropPosition, so lit, shadow, depth, normals and motion
// vectors all see the same plant.
#ifndef HYPERRTS_ROW_CROP_PASSES_INCLUDED
#define HYPERRTS_ROW_CROP_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/RowCrops/RowCropInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

// Published by the scene's GroundColor (x min, z min, 1 / width, 1 / depth); a zero rect turns root blending off.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

float3 _LightDirection;
float3 _LightPosition;

struct ForwardVaryings
{
    float4 positionCS : SV_POSITION;
    float3 uv : TEXCOORD0; // xy albedo, z metres up the plant
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half3 tint : TEXCOORD3;
    half4 ground : TEXCOORD4; // rgb ground colour over the texture's mean brightness, a root blend
    half fogFactor : TEXCOORD5;
};

struct CutoutVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
};

struct MotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
};

half4 RootGround(float3 origin)
{
    float2 uv = (origin.xz - _HyperRTSGroundRect.xy) * _HyperRTSGroundRect.zw;
    half3 ground = SAMPLE_TEXTURE2D_LOD(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, uv, 0).rgb;
    half mean = Luminance(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 16).rgb);
    half weight = _HyperRTSGroundRect.z > 0.0 ? _GroundBlend : 0.0;
    return half4(ground / max(mean, 0.02), weight);
}

ForwardVaryings ForwardVert(Attributes input)
{
    ForwardVaryings output;
    RowCropInstance plant = _Instances[input.instanceID];
    output.positionWS = RowCropPosition(input, plant, WindThisFrame());
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = float3(TRANSFORM_TEX(input.uv, _BaseMap), input.positionOS.y * plant.scale);
    output.normalWS = RowCropNormal(input, plant);

    // Each plant gets its own slightly warmer or cooler, lighter or darker shade.
    half random = plant.shade;
    output.tint = lerp(half3(1.0, 1.0, 1.0), half3(1.12, 1.02, 0.72), random * _HueVariation * 4.0)
        * (1.0 - _HueVariation + random * _HueVariation * 2.0);
    output.ground = RootGround(plant.position);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

half4 ForwardFrag(ForwardVaryings input) : SV_Target
{
    half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
    half4 albedo = texel * _BaseColor;
    clip(albedo.a - _Cutoff);
    albedo.rgb *= input.tint;

    // Toward the root the stem turns into the ground it grows from, in colour and in light.
    half root = input.ground.a * (1.0 - smoothstep(0.0, _GroundBlendHeight, input.uv.z));
    albedo.rgb = lerp(albedo.rgb, input.ground.rgb * Luminance(texel.rgb), root);

    half3 normal = normalize(lerp(input.normalWS, half3(0.0, 1.0, 0.0), root));
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
    half wrapping = 0.5 * (1.0 - root);
    half wrap = saturate((dot(normal, sun.direction) + wrapping) / (1.0 + wrapping));
    half through = pow(saturate(dot(-sun.direction, view)), 4.0) * _Translucency * (1.0 - root);
    half3 halfway = normalize(sun.direction + view);
    half glint = pow(saturate(dot(normal, halfway)), exp2(_Smoothness * 10.0 + 1.0)) * _Smoothness;

    half occlusion = 1.0;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        occlusion = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
    #endif

    half3 ambient = SampleSH(normal) * occlusion;
    half3 direct = sun.color * sun.shadowAttenuation;
    half3 color = albedo.rgb * (direct * (wrap + through) + ambient) + glint * direct;
    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0);
}

CutoutVaryings CutoutVert(Attributes input)
{
    CutoutVaryings output;
    RowCropInstance plant = _Instances[input.instanceID];
    output.positionCS = TransformWorldToHClip(RowCropPosition(input, plant, WindThisFrame()));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = RowCropNormal(input, plant);
    return output;
}

CutoutVaryings ShadowVert(Attributes input)
{
    CutoutVaryings output;
    RowCropInstance plant = _Instances[input.instanceID];
    float3 world = RowCropPosition(input, plant, WindThisFrame());
    float3 normal = RowCropNormal(input, plant);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 toLight = normalize(_LightPosition - world);
    #else
        float3 toLight = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(world, normal, toLight));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = normal;
    return output;
}

half4 ShadowFrag(CutoutVaryings input) : SV_Target
{
    Coverage(input.uv);
    return 0;
}

half4 DepthFrag(CutoutVaryings input) : SV_Target
{
    Coverage(input.uv);
    return input.positionCS.z;
}

half4 DepthNormalsFrag(CutoutVaryings input) : SV_Target
{
    Coverage(input.uv);
    float3 normal = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octahedral = PackNormalOctQuadEncode(normal);
        return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
    #else
        return half4(normal, 0.0);
    #endif
}

// Plants don't move, so last frame's position only rebuilds last frame's wind.
MotionVaryings MotionVert(Attributes input)
{
    MotionVaryings output;
    RowCropInstance plant = _Instances[input.instanceID];
    float3 now = RowCropPosition(input, plant, WindThisFrame());
    float3 last = RowCropPosition(input, plant, WindLastFrame());
    output.positionCS = TransformWorldToHClip(now);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(now, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(last, 1.0));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

float4 MotionFrag(MotionVaryings input) : SV_Target
{
    Coverage(input.uv);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif
