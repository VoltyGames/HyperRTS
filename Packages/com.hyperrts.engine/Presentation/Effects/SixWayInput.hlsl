// Material inputs and vertex layout of ParticlesSixWay.shader. Needs URP Core.hlsl first.
#ifndef HYPERRTS_SIX_WAY_INPUT_INCLUDED
#define HYPERRTS_SIX_WAY_INPUT_INCLUDED

TEXTURE2D(_PositiveMap);
SAMPLER(sampler_PositiveMap);
TEXTURE2D(_NegativeMap);
SAMPLER(sampler_NegativeMap);
TEXTURE2D(_EmissionGradient);

CBUFFER_START(UnityPerMaterial)
    half _Density;
    half4 _EmissionColor;
    half _AmbientStrength;
    float _SoftDistance;
    float _NearFadeStart;
    float _NearFadeEnd;
CBUFFER_END

// Shuriken packs the custom streams in order: UV and UV2 share TEXCOORD0, AnimBlend and Custom1.x share TEXCOORD1.
struct SixWayAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    half4 color : COLOR;
    float4 frames : TEXCOORD0;
    float2 blendAndEmission : TEXCOORD1;
};

struct SixWayVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float4 frames : TEXCOORD1;
    half4 color : TEXCOORD2;
    half2 blendAndEmission : TEXCOORD3;
    half3 tangentWS : TEXCOORD4;
    half3 bitangentWS : TEXCOORD5;
    half3 normalWS : TEXCOORD6;
    half3 ambientUp : TEXCOORD7;
    half3 ambientDown : TEXCOORD8;
    half3 ambientSide : TEXCOORD9;
};

#endif
