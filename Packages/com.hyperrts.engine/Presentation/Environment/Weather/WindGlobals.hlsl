// The wind globals the scene's Wind publishes.
#ifndef HYPERRTS_WIND_GLOBALS_INCLUDED
#define HYPERRTS_WIND_GLOBALS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float4 _HyperRTSWindDirection;  // xz unit downwind
float _HyperRTSWindStrength;    // the wind zone's main strength
float4 _HyperRTSWindGust;       // x gust amount, y turbulence, z gust tiles per metre
float4 _HyperRTSWindGustOffset; // xy this frame's gust scroll (tiles), zw last frame's
float _HyperRTSWindDetail;      // 0 none, 1 sway, 2 + branches, 3 + flutter
#define HYPERRTS_MAX_GUSTS 8
float4 _HyperRTSGusts[HYPERRTS_MAX_GUSTS]; // rotor downwash and blasts: xy centre, z radius, w push at the centre
float _HyperRTSGustCount;
TEXTURE2D(_HyperRTSWindGustMap);
SAMPLER(sampler_HyperRTSWindGustMap);

#endif
