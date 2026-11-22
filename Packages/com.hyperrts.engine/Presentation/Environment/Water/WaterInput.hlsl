// Water.shader's material properties: the ripple normal map and the UnityPerMaterial layout every pass shares.
#ifndef HYPERRTS_WATER_INPUT_INCLUDED
#define HYPERRTS_WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

CBUFFER_START(UnityPerMaterial)
    half4 _ScatterColor;
    half4 _Absorption;
    half _BedDarkening;
    half _Caustics;
    float4 _NormalMap_ST;
    float _RippleSize;
    half _RippleStrength;
    float _RippleFadeStart;
    float _RippleFadeEnd;
    half _RippleStrengthFar;
    float _FlowSpeed;
    half _Smoothness;
    half _SmoothnessFar;
    float _SmoothnessFarDistance;
    half _Refraction;
    half _GlintStrength;
    half _GlintPeak;
    float _GlintFadeStart;
    float _GlintFadeEnd;
    float _ShoreFadeDepth;
    half4 _FoamColor;
    half _FordAlpha;
    float _RapidsFoamWidth;
    float _RapidsFoamLength;
    half _FlowFoamAmount;
    float _FlowFoamScale;
    float _BankFoamWidth;
    half _BankFoamAmount;
    half _ShoreWaveFoam;
    float _ShoreWaveLength;
    float _ShoreWavePeriod;
    half _ShoreLap;
    half _WakeFoam;
    half _WakeRipple;
    half _ReflectionStrength;
    half _ReflectionFloor;
    float _ReflectionDistance;
    float _ReflectionThickness;
    half _ReflectionDistortion;
CBUFFER_END

#endif
