// The interaction map globals InteractionSurfaces publishes. Channel layout: docs/modules/client.md, Interaction maps.
#ifndef HYPERRTS_INTERACTION_GLOBALS_INCLUDED
#define HYPERRTS_INTERACTION_GLOBALS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

TEXTURE2D(_HyperRTSTrample);
TEXTURE2D(_HyperRTSTracks);
TEXTURE2D(_HyperRTSWake);
float4 _HyperRTSInteractionRect; // x min, z min, 1 / width, 1 / depth; zero = no maps
float4 _HyperRTSTracksTexel;     // texels in x and z, metres per texel in x and z
float _HyperRTSMaxCraterDepth;   // metres a full tracks alpha means

#endif
