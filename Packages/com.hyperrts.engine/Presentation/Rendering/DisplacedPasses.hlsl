// Shadow, depth, depth-normals and motion vector passes for a shader that moves its vertices. Define first: Attributes,
// Displace(input, frame, objectToWorld, out positionWS, out normalWS, out uv) and ClipDisplaced(uv, positionCS).
// DISPLACED_VIEW_FADE(positionCS) may dither away the colour and depth passes' pixels, never the shadow's.
// DISPLACED_FACING_NORMALS turns back-face normals toward the eye in the depth-normals texture.
#ifndef HYPERRTS_DISPLACED_PASSES_INCLUDED
#define HYPERRTS_DISPLACED_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wind.hlsl"

#if !defined(DISPLACED_VIEW_FADE)
    #define DISPLACED_VIEW_FADE(positionCS)
#endif

struct DisplacedVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DisplacedMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

DisplacedVaryings DepthVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    DisplacedVaryings output = (DisplacedVaryings)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS;
    Displace(input, WindThisFrame(), UNITY_MATRIX_M, positionWS, output.normalWS, output.uv);
    output.positionCS = TransformWorldToHClip(positionWS);
    return output;
}

DisplacedVaryings ShadowVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    DisplacedVaryings output = (DisplacedVaryings)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS;
    Displace(input, WindThisFrame(), UNITY_MATRIX_M, positionWS, output.normalWS, output.uv);
    output.positionCS = ShadowCasterPositionCS(positionWS, output.normalWS);
    return output;
}

// Last frame's position comes from last frame's wind and object matrix, so sway and toppling both show.
DisplacedMotionVaryings MotionVert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    DisplacedMotionVaryings output = (DisplacedMotionVaryings)0;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 now;
    float3 last;
    half3 normalWS;
    float2 uv;
    Displace(input, WindThisFrame(), UNITY_MATRIX_M, now, normalWS, output.uv);
    Displace(input, WindLastFrame(), UNITY_PREV_MATRIX_M, last, normalWS, uv);
    output.positionCS = TransformWorldToHClip(now);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(now, 1.0));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(last, 1.0));
    return output;
}

half4 ShadowFrag(DisplacedVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipDisplaced(input.uv, input.positionCS);
    return 0;
}

half4 DepthFrag(DisplacedVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipDisplaced(input.uv, input.positionCS);
    DISPLACED_VIEW_FADE(input.positionCS);
    return input.positionCS.z;
}

half4 DepthNormalsFrag(DisplacedVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipDisplaced(input.uv, input.positionCS);
    DISPLACED_VIEW_FADE(input.positionCS);
    half3 normalWS = normalize(input.normalWS);
#if defined(DISPLACED_FACING_NORMALS)
    normalWS *= IS_FRONT_VFACE(face, 1.0, -1.0);
#endif
    return DepthNormalOutput(normalWS);
}

float4 MotionFrag(DisplacedMotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipDisplaced(input.uv, input.positionCS);
    DISPLACED_VIEW_FADE(input.positionCS);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif
