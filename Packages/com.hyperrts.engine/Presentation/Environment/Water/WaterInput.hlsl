// Water.shader's material properties: the ripple and foam maps and the UnityPerMaterial layout every pass shares.
#ifndef HYPERRTS_WATER_INPUT_INCLUDED
#define HYPERRTS_WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_FoamMap);
SAMPLER(sampler_FoamMap);

CBUFFER_START(UnityPerMaterial)
    half4 _ShallowColor;
    half4 _DeepColor;
    float _DeepDepth;
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
    float _SlickWidth;
    float _SlickLength;
    half _SlickCalm;
    float _GustSize;
    float _GustSpeed;
    half _GustStrength;
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
    float _FoamSize;
    half _FordAlpha;
    half _FlowFoamAmount;
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
    float _PierWakeLength;
    half _PierWakeFoam;
    half _PierChop;
    float _PierCushionWidth;
CBUFFER_END

// The map's bridge piers as (world x, world z, half-width across the flow), set as globals by WaterPiers, whose
// Capacity MAX_WATER_PIERS matches.
#define MAX_WATER_PIERS 16
float4 _HyperRTSWaterPiers[MAX_WATER_PIERS];
float _HyperRTSWaterPierCount;

#endif
