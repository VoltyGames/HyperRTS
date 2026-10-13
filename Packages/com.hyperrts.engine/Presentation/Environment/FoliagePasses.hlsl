// HyperRTS/Foliage passes. Every pass places vertices with FoliagePosition, so lit, shadow, depth, normals and motion
// vectors all see the same windblown plant.
#ifndef HYPERRTS_FOLIAGE_PASSES_INCLUDED
#define HYPERRTS_FOLIAGE_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/FoliageInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

// The map's top-down ground colour and the world rectangle it covers (x min, z min, 1 / width, 1 / depth), published
// by the scene's GroundColor. Outside a map the rect stays zero, which turns root blending off.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

// One tile of the noise per 40 m: patches of drier and greener plants.
static const float PatchTilesPerMetre = 1.0 / 40.0;

float3 _LightDirection;
float3 _LightPosition;

struct ForwardVaryings
{
    float4 positionCS : SV_POSITION;
    float3 uv : TEXCOORD0; // xy albedo, z height up the card (the root is 0)
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tint : TEXCOORD3; // rgb per-plant shade, a crown occlusion
    half4 ground : TEXCOORD4; // rgb ground colour over the card's mean brightness, a root blend
    half fogFactor : TEXCOORD5;
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

// The ground under the plant's root, divided by the card's mean brightness so the root keeps the card's own light and
// dark detail; the blend weight is zero outside a map.
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
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 origin;
    output.positionWS = FoliagePosition(input, WindThisFrame(), origin);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = float3(TRANSFORM_TEX(input.uv, _BaseMap), input.uv.y);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    // Plants dry out in patches tens of metres across (the gust noise, held still) and each plant gets its own slightly
    // warmer or cooler, lighter or darker shade on top.
    float random = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
    half patch = SAMPLE_TEXTURE2D_LOD(_HyperRTSWindGustMap, sampler_HyperRTSWindGustMap, origin.xz * PatchTilesPerMetre, 0).r;
    half dry = saturate(patch * 0.65 + random * 0.35);
    half3 shade = lerp(half3(1.0, 1.0, 1.0), half3(1.12, 1.02, 0.72), dry * _HueVariation * 4.0)
        * (1.0 - _HueVariation + random * _HueVariation * 2.0);
    output.tint = half4(shade, lerp(1.0, input.color.r, _VertexOcclusion));
    output.ground = RootGround(origin);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

half4 ForwardFrag(ForwardVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
    half4 albedo = texel * _BaseColor;
    clip(albedo.a - _Cutoff);
    FoliageCrossFade(input.positionCS);
    albedo.rgb *= input.tint.rgb;

    // Toward the root the card turns into the ground it grows from, in colour and in light (plain Lambert on the
    // ground's up normal, nothing shining through), so grass rises out of the ground.
    half root = input.ground.a * (1.0 - smoothstep(0.0, _GroundBlendHeight, input.uv.z));
    albedo.rgb = lerp(albedo.rgb, input.ground.rgb * Luminance(texel.rgb), root);

    // Not flipped on back faces: foliage normals point up and out of the plant, so both sides of a card light like
    // the plant as a whole.
    half3 normal = normalize(lerp(input.normalWS, half3(0.0, 1.0, 0.0), root));
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
    // Shadow and cookie only: the sun's culling attenuation isn't set for the terrain's instanced detail draws.
    half shadow = sun.shadowAttenuation;
    half wrapping = 0.5 * (1.0 - root);
    half wrap = saturate((dot(normal, sun.direction) + wrapping) / (1.0 + wrapping));
    half crown = input.tint.a;
    half through = pow(saturate(dot(-sun.direction, view)), 4.0) * _Translucency * crown * (1.0 - root);
    half3 halfway = normalize(sun.direction + view);
    half glint = pow(saturate(dot(normal, halfway)), exp2(_Smoothness * 10.0 + 1.0)) * _Smoothness;

    half occlusion = crown;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        occlusion *= GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS)).indirectAmbientOcclusion;
    #endif

    half3 ambient = SampleSH(normal) * occlusion;
    half3 direct = sun.color * shadow;
    half3 color = albedo.rgb * (direct * (wrap * crown + through) + ambient) + glint * crown * direct;
    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0);
}

CutoutVaryings CutoutVert(Attributes input)
{
    CutoutVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 origin;
    output.positionCS = TransformWorldToHClip(FoliagePosition(input, WindThisFrame(), origin));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

CutoutVaryings ShadowVert(Attributes input)
{
    CutoutVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 origin;
    float3 world = FoliagePosition(input, WindThisFrame(), origin);
    float3 normal = TransformObjectToWorldNormal(input.normalOS);
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
    UNITY_SETUP_INSTANCE_ID(input);
    Coverage(input.uv);
    FoliageCrossFade(input.positionCS);
    return 0;
}

half4 DepthFrag(CutoutVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    Coverage(input.uv);
    FoliageCrossFade(input.positionCS);
    return input.positionCS.z;
}

half4 DepthNormalsFrag(CutoutVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    Coverage(input.uv);
    FoliageCrossFade(input.positionCS);
    float3 normal = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octahedral = PackNormalOctQuadEncode(normal);
        return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
    #else
        return half4(normal, 0.0);
    #endif
}

// Last frame's position rebuilds the wind from last frame's time and gust scroll.
MotionVaryings MotionVert(Attributes input)
{
    MotionVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 origin;
    float3 now = FoliagePosition(input, WindThisFrame(), origin);
    float3 last = FoliagePosition(input, WindLastFrame(), origin);
    output.positionCS = TransformWorldToHClip(now);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(now, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(last, 1.0));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

float4 MotionFrag(MotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    Coverage(input.uv);
    FoliageCrossFade(input.positionCS);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif
