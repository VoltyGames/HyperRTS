// HyperRTS/Bark's motion vector pass, through DisplacedPasses.hlsl so last frame's wind poses last frame. Include
// BarkInput.hlsl first.
#ifndef HYPERRTS_BARK_MOTION_PASS_INCLUDED
#define HYPERRTS_BARK_MOTION_PASS_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/ViewFade.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float2 uv4 : TEXCOORD4;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

void Displace(Attributes input, WindFrame frame, float4x4 objectToWorld, out float3 positionWS, out half3 normalWS,
    out float2 uv)
{
    float3 positionOS = BarkWindPosition(input.positionOS.xyz, input.uv2, input.uv3, input.uv4, input.color, frame);
    positionWS = mul(objectToWorld, float4(positionOS, 1.0)).xyz;
    normalWS = TransformObjectToWorldNormal(input.normalOS);
    uv = input.texcoord;
}

void ClipDisplaced(float2 uv, float4 positionCS)
{
    LODFadeCrossFade(positionCS);
    TreeViewFade(positionCS);
}

#include "Packages/com.hyperrts.engine/Presentation/Rendering/DisplacedPasses.hlsl"

#endif
