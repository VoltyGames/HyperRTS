// Height fog then fog of war, the order the full-screen passes lay them on the opaques. Needs URP Core.hlsl first.
#ifndef HYPERRTS_SCENE_FOG_INCLUDED
#define HYPERRTS_SCENE_FOG_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/HeightFog.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

// Both fogs over one world point, each as colour (rgb) and amount (a).
struct SceneFog
{
    half4 height;
    half4 war;
};

SceneFog SceneFogAt(float3 positionWS)
{
    SceneFog fog;
    fog.height = HeightFogAt(positionWS);
    fog.war = FogOfWarAt(positionWS);
    return fog;
}

half3 ApplyFog(half3 color, half4 fog)
{
    return lerp(color, fog.rgb, fog.a);
}

half3 ApplySceneFog(half3 color, SceneFog fog)
{
    return ApplyFog(ApplyFog(color, fog.height), fog.war);
}

#endif
