// HyperRTS/Bark passes: URP Lit's own passes behind vertex entries that move the vertex with the tree wind in object
// space first, so every pass sees the same windblown tree. Each pass defines one BARK_PASS_* before including this.
#ifndef HYPERRTS_BARK_PASSES_INCLUDED
#define HYPERRTS_BARK_PASSES_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/BarkInput.hlsl"

#if defined(BARK_PASS_FORWARD)
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

Varyings BarkForwardVertex(BarkAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    Attributes lit = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(input, lit);
    lit.positionOS = float4(BarkWindPosition(input, WindThisFrame()), 1.0);
    lit.normalOS = input.normalOS;
    lit.tangentOS = input.tangentOS;
    lit.texcoord = input.texcoord;
    return LitPassVertex(lit);
}
#endif

#if defined(BARK_PASS_SHADOW)
#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

Varyings BarkShadowVertex(BarkAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    Attributes lit = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(input, lit);
    lit.positionOS = float4(BarkWindPosition(input, WindThisFrame()), 1.0);
    lit.normalOS = input.normalOS;
    lit.texcoord = input.texcoord;
    return ShadowPassVertex(lit);
}
#endif

#if defined(BARK_PASS_DEPTH)
#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

Varyings BarkDepthVertex(BarkAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    Attributes lit = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(input, lit);
    lit.position = float4(BarkWindPosition(input, WindThisFrame()), 1.0);
    lit.texcoord = input.texcoord;
    return DepthOnlyVertex(lit);
}
#endif

#if defined(BARK_PASS_DEPTH_NORMALS)
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

Varyings BarkDepthNormalsVertex(BarkAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    Attributes lit = (Attributes)0;
    UNITY_TRANSFER_INSTANCE_ID(input, lit);
    lit.positionOS = float4(BarkWindPosition(input, WindThisFrame()), 1.0);
    lit.tangentOS = input.tangentOS;
    lit.texcoord = input.texcoord;
    lit.normal = input.normalOS;
    return DepthNormalsVertex(lit);
}
#endif

#if defined(BARK_PASS_MOTION)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct BarkMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Last frame's position rebuilds the wind from last frame's time and gust scroll.
BarkMotionVaryings BarkMotionVertex(BarkAttributes input)
{
    BarkMotionVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float4 now = float4(BarkWindPosition(input, WindThisFrame()), 1.0);
    float4 last = float4(BarkWindPosition(input, WindLastFrame()), 1.0);
    output.positionCS = TransformObjectToHClip(now.xyz);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, now));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, last));
    return output;
}

float4 BarkMotionFragment(BarkMotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}
#endif

#endif
