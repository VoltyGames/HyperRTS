// The trample map globals TrampleSurfaces publishes. Channel layout: docs/modules/client.md, Trample maps.
#ifndef HYPERRTS_INTERACTION_GLOBALS_INCLUDED
#define HYPERRTS_INTERACTION_GLOBALS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/WorldRect.hlsl"

// Metres a full tracks alpha means; deeper craters clip. Matches TrampleStamps.MaxCraterDepth.
#define HYPERRTS_MAX_CRATER_DEPTH 2.0

// The rut ramp the brush stamps rises by RUT_RAMP across the rut; the ground reads it as rut from RUT_EDGE and fully
// dark from RUT_FULL, so the edge sits at 1 - RUT_EDGE / RUT_RAMP of the half width.
#define RUT_RAMP 1.5
#define RUT_EDGE 0.1
#define RUT_FULL 0.4

TEXTURE2D(_HyperRTSTrample);
TEXTURE2D(_HyperRTSTracks);
TEXTURE2D(_HyperRTSWake);
TEXTURE2D(_HyperRTSRemains);   // R rubble, G ash, B fuel, A wet mud
float4 _HyperRTSTrampleRect; // x min, z min, 1 / width, 1 / depth; zero = no maps
float4 _HyperRTSTracksTexel;     // texels in x and z, metres per texel in x and z

bool TrampleMapsOn()
{
    return _HyperRTSTrampleRect.z > 0.0;
}

// UV on the trample maps; outside 0 to 1 is off them.
float2 TrampleUV(float2 positionXZ)
{
    return RectUV(_HyperRTSTrampleRect, positionXZ);
}

bool OnTrampleMaps(float2 uv)
{
    return TrampleMapsOn() && all(uv == saturate(uv));
}

#endif
