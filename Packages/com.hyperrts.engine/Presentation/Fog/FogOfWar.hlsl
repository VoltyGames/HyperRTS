// Fog of war from FogOfWarRenderer's globals, for the full-screen pass and transparents. Needs URP Core.hlsl first.
#ifndef HYPERRTS_FOG_OF_WAR_INCLUDED
#define HYPERRTS_FOG_OF_WAR_INCLUDED

TEXTURE2D(_HyperRTSFogOfWar);
SAMPLER(sampler_HyperRTSFogOfWar);
float4 _HyperRTSFogOfWarRect;   // x min x, y min z, z size x, w size z
half4 _HyperRTSFogOfWarColor;   // a 0 when no fog is shown
float4 _HyperRTSFogOfWarBorder; // x opacity beyond the map, y metres it fades in over, zw one cell in uv
half4 _HyperRTSFogOfWarLook;    // x how much colour the fog drains
TEXTURE2D(_HyperRTSSightShape);
SAMPLER(sampler_HyperRTSSightShape);
half4 _HyperRTSSightShapeColor; // a 0 when no outline is shown

// The grid's opacity blurred over about a cell, so its stair steps read as round edges.
half FogOfWarOpacity(float2 uv)
{
    float2 cell = _HyperRTSFogOfWarBorder.zw * 0.7;
    half centre = SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv), 0).r;
    half around = SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv + cell), 0).r
        + SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv - cell), 0).r
        + SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv + float2(cell.x, -cell.y)), 0).r
        + SAMPLE_TEXTURE2D_LOD(_HyperRTSFogOfWar, sampler_HyperRTSFogOfWar, saturate(uv + float2(-cell.x, cell.y)), 0).r;
    return centre * 0.4 + around * 0.15;
}

// Fog colour (rgb) and amount (a) over a world point.
half4 FogOfWarAt(float3 positionWS)
{
    if (_HyperRTSFogOfWarColor.a <= 0.0)
    {
        return half4(0.0, 0.0, 0.0, 0.0);
    }

    float2 uv = (positionWS.xz - _HyperRTSFogOfWarRect.xy) / _HyperRTSFogOfWarRect.zw;
    half opacity = FogOfWarOpacity(uv);

    // Scenery past the playable edge stays dim whatever the grid says.
    float2 outside = max(_HyperRTSFogOfWarRect.xy - positionWS.xz, positionWS.xz - (_HyperRTSFogOfWarRect.xy + _HyperRTSFogOfWarRect.zw));
    opacity = max(opacity, _HyperRTSFogOfWarBorder.x * saturate(max(outside.x, outside.y) / _HyperRTSFogOfWarBorder.y));
    return half4(_HyperRTSFogOfWarColor.rgb, opacity * _HyperRTSFogOfWarColor.a);
}

// What fog found by FogOfWarAt makes of a lit colour: drained of colour, then darkened toward the fog colour.
half3 ApplyFogOfWar(half3 color, half4 fog)
{
    half grey = dot(color, half3(0.2126, 0.7152, 0.0722));
    half3 muted = lerp(color, grey.xxx, saturate(fog.a * 2.0) * _HyperRTSFogOfWarLook.x);
    return lerp(muted, fog.rgb, fog.a);
}

// A thin line round the ground the selected units see, about a quarter of a cell wide. Its width is set on the
// ground, not on screen, so crops and hedges don't break it into speckle.
half3 SightShapeOutline(half3 color, float3 positionWS)
{
    float2 uv = (positionWS.xz - _HyperRTSFogOfWarRect.xy) / _HyperRTSFogOfWarRect.zw;
    half mask = SAMPLE_TEXTURE2D_LOD(_HyperRTSSightShape, sampler_HyperRTSSightShape, saturate(uv), 0).r;
    half edge = saturate(1.0 - abs(mask - 0.5) / 0.12) * _HyperRTSSightShapeColor.a;
    return lerp(color, _HyperRTSSightShapeColor.rgb, edge);
}

#endif
