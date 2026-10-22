// Fog of war from FogOfWarRenderer's globals, for the full-screen pass and transparents. Needs URP Core.hlsl first.
#ifndef HYPERRTS_FOG_OF_WAR_INCLUDED
#define HYPERRTS_FOG_OF_WAR_INCLUDED

TEXTURE2D(_HyperRTSFogOfWar);
SAMPLER(sampler_HyperRTSFogOfWar);
float4 _HyperRTSFogOfWarRect;   // x min x, y min z, z size x, w size z
half4 _HyperRTSFogOfWarColor;   // a 0 when no fog is shown
float4 _HyperRTSFogOfWarBorder; // x opacity beyond the map, y metres it fades in over

// Fog colour (rgb) and amount (a) over a world point.
half4 FogOfWarAt(float3 positionWS)
{
    if (_HyperRTSFogOfWarColor.a <= 0.0)
    {
        return half4(0.0, 0.0, 0.0, 0.0);
    }

    float2 uv = (positionWS.xz - _HyperRTSFogOfWarRect.xy) / _HyperRTSFogOfWarRect.zw;
    half opacity = SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv), 0).r;

    // Scenery past the playable edge stays dim whatever the grid says.
    float2 outside = max(_HyperRTSFogOfWarRect.xy - positionWS.xz, positionWS.xz - (_HyperRTSFogOfWarRect.xy + _HyperRTSFogOfWarRect.zw));
    opacity = max(opacity, _HyperRTSFogOfWarBorder.x * saturate(max(outside.x, outside.y) / _HyperRTSFogOfWarBorder.y));
    return half4(_HyperRTSFogOfWarColor.rgb, opacity * _HyperRTSFogOfWarColor.a);
}

half3 ApplyFogOfWar(half3 color, half4 fog)
{
    return lerp(color, fog.rgb, fog.a);
}

#endif
