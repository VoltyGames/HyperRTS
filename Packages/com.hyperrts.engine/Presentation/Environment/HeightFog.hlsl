// Exponential height fog from HeightFog.cs, integrated along the view ray. Needs URP Core.hlsl first.
#ifndef HYPERRTS_HEIGHT_FOG_INCLUDED
#define HYPERRTS_HEIGHT_FOG_INCLUDED

float4 _HyperRTSFogColor;
float4 _HyperRTSFogSunColor;
float4 _HyperRTSFogParams; // x density per metre at base height, y base height, z falloff per metre, w start distance
float4 _HyperRTSFogShape;  // x max opacity (0 when no fog), y sun exponent

// (1 - e^-x) / x: the fraction of base density a ray that climbs x falloff lengths sees. Near 0 (level rays) the
// division is unstable, so use its series.
float HeightFogClimb(float x)
{
    x = clamp(x, -80.0, 80.0);
    if (abs(x) < 0.01)
    {
        return 1.0 - 0.5 * x;
    }

    return (1.0 - exp(-x)) / x;
}

// Fog colour (rgb) and amount (a) between the camera and positionWS, capped at the far plane.
half4 HeightFogAt(float3 positionWS)
{
    float3 ray = positionWS - _WorldSpaceCameraPos;
    float rayLength = length(ray);
    float start = _HyperRTSFogParams.w;
    if (rayLength <= start || _HyperRTSFogShape.x <= 0.0)
    {
        return half4(0.0, 0.0, 0.0, 0.0);
    }

    float3 direction = ray / rayLength;
    rayLength = min(rayLength, _ProjectionParams.z);
    float fogged = max(rayLength - start, 0.0);
    float startHeight = _WorldSpaceCameraPos.y + direction.y * start;
    float falloff = _HyperRTSFogParams.z;
    float densityAtStart = _HyperRTSFogParams.x * exp(min(-falloff * (startHeight - _HyperRTSFogParams.y), 80.0));
    float opticalDepth = densityAtStart * fogged * HeightFogClimb(falloff * direction.y * fogged);
    half amount = min(1.0 - exp(-opticalDepth), _HyperRTSFogShape.x);

    half sun = pow(saturate(dot(direction, _MainLightPosition.xyz)), _HyperRTSFogShape.y);
    half3 color = _HyperRTSFogColor.rgb + _HyperRTSFogSunColor.rgb * sun;
    return half4(color, amount);
}

// Undoes fog already in a colour, for transparents that sample the fogged opaque texture and fog again.
half3 RemoveHeightFog(half3 color, half4 fog)
{
    return max(color - fog.rgb * fog.a, 0.0) / max(1.0 - fog.a, 0.01);
}

#endif
