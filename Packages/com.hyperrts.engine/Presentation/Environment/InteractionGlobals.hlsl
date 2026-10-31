// The interaction map globals InteractionSurfaces publishes. Channel layout: docs/modules/client.md, Interaction maps.
#ifndef HYPERRTS_INTERACTION_GLOBALS_INCLUDED
#define HYPERRTS_INTERACTION_GLOBALS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/WorldRect.hlsl"

// Metres a full tracks alpha means; deeper craters clip. Matches InteractionStamps.MaxCraterDepth.
#define HYPERRTS_MAX_CRATER_DEPTH 2.0

TEXTURE2D(_HyperRTSTrample);
TEXTURE2D(_HyperRTSTracks);
TEXTURE2D(_HyperRTSWake);
TEXTURE2D(_HyperRTSRemains);   // R rubble, G ash, B fuel, A wet mud
float4 _HyperRTSInteractionRect; // x min, z min, 1 / width, 1 / depth; zero = no maps
float4 _HyperRTSTracksTexel;     // texels in x and z, metres per texel in x and z

bool InteractionMapsOn()
{
    return _HyperRTSInteractionRect.z > 0.0;
}

// UV on the interaction maps; outside 0 to 1 is off them.
float2 InteractionUV(float2 positionXZ)
{
    return RectUV(_HyperRTSInteractionRect, positionXZ);
}

bool OnInteractionMaps(float2 uv)
{
    return InteractionMapsOn() && all(uv == saturate(uv));
}

#endif
