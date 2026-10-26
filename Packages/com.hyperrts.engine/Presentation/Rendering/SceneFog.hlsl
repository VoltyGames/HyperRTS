// URP's fog then the fog of war, the order the full-screen passes lay them on the opaques. Needs URP Core.hlsl and
// "#pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC" first.
#ifndef HYPERRTS_SCENE_FOG_INCLUDED
#define HYPERRTS_SCENE_FOG_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VolumetricFog.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

// Both fogs over one world point: URP's as exposed in-scattered light (rgb) and opacity (a), the fog of war as
// colour (rgb) and amount (a).
struct SceneFog
{
    half4 atmosphere;
    half4 war;
};

// URP's fog between the camera and a point seen at uv and eye depth, as its apply pass computes it.
half4 AtmosphereFogAt(float2 uv, float3 positionWS, float eyeDepth)
{
    float3 toPoint = positionWS - _WorldSpaceCameraPos.xyz;
    float distance = length(toPoint);
    float3 radiance = 0.0;
    float opacity = 0.0;
    float volumeEnd = 0.0;
#if defined(_FOG_VOLUMETRIC)
    float4 volume = DelinearizeRGBD(SampleVBufferTrilinear(uv, VolumetricFogSliceCoord(eyeDepth)));
    radiance = volume.rgb;
    opacity = OpacityFromOpticalDepth(volume.a);
    volumeEnd = _VBufferLastSliceDist;
#endif
    ApplyAnalyticFog(toPoint.y / distance, _WorldSpaceCameraPos.y, volumeEnd, distance, radiance, opacity);
    return half4(radiance * GetPreExposureMultiplier(), opacity);
}

// positionCS is the fragment's SV_Position, whose w is its eye depth in perspective.
SceneFog SceneFogAt(float3 positionWS, float4 positionCS)
{
    SceneFog fog;
    fog.atmosphere = AtmosphereFogAt(GetNormalizedScreenSpaceUV(positionCS), positionWS, positionCS.w);
    fog.war = FogOfWarAt(positionWS);
    return fog;
}

half3 ApplyAtmosphereFog(half3 color, half4 fog)
{
    return color * (1.0 - fog.a) + fog.rgb;
}

// Undoes URP's fog already in a colour, for transparents that sample the fogged opaque texture and fog again. The
// gain stops at 4, so a point deep in fog isn't brightened past what its fog let through.
half3 RemoveAtmosphereFog(half3 color, half4 fog)
{
    return max(color - fog.rgb, 0.0) / max(1.0 - fog.a, 0.25);
}

half3 ApplyFog(half3 color, half4 fog)
{
    return lerp(color, fog.rgb, fog.a);
}

half3 ApplySceneFog(half3 color, SceneFog fog)
{
    return ApplyFog(ApplyAtmosphereFog(color, fog.atmosphere), fog.war);
}

#endif
