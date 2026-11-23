// Water.shader's surface shading: ripples, slicks, gusts, caustics, smoothness, glint and cloud shade. Include after
// the water's CBUFFER and maps and URP's Lighting.hlsl.
#ifndef HYPERRTS_WATER_SURFACE_INCLUDED
#define HYPERRTS_WATER_SURFACE_INCLUDED

#define WATER_CAUSTIC_DEPTH 1.2 // metres of water caustics reach through
#define WATER_RIPPLE_DRAWN 0.4 // how much shorter ripples are across the current than along it
#define WATER_STILL_DRIFT 0.1 // m/s the wind pushes ripples over still water
#define WATER_GUST_DIRECTION float2(0.8, 0.6) // the wind the ripple spectrum was made for

// 1 before start metres, 0 past end.
half Fade(float metres, float start, float end)
{
    return 1.0 - saturate((metres - start) / max(end - start, 0.001));
}

half Brightest(half3 color)
{
    return max(color.r, max(color.g, color.b));
}

// The foam map's broad blobs (B): smooth enough to read at the top mip, so branches may call it.
half Blobs(float2 p)
{
    return SAMPLE_TEXTURE2D_LOD(_FoamMap, sampler_FoamMap, p, 0).b;
}

// 1 along calm slicks: long streaks drifting down the current (V), where the surface film smooths the ripples.
half Slick(float2 uv, float drift)
{
    return Blobs(float2(uv.x / _SlickWidth, (uv.y - drift * 0.8) / _SlickLength));
}

// 0..1 wind gusts darkening the water as patches that sweep across it, whatever way the river runs.
half Gust(float3 positionWS)
{
    return Blobs((positionWS.xz - WATER_GUST_DIRECTION * _Time.y * _GustSpeed) / _GustSize);
}

// One ripple scale carried travel metres downstream (V), turned so the scales don't line up.
half2 RippleLayer(float2 uv, float size, float2x2 turn, float travel, half strength)
{
    float2 p = mul(turn, uv - float2(0.0, travel)) / size;
    half2 slope = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p), strength).xy;
    return mul(transpose(turn), slope);
}

// Three scales of the wave spectrum: long swells, ripples and a fine chop, the chop gone by mid range. travel is how
// far the current has carried the surface, the same everywhere on a sheet so the ripples never shear.
half3 Ripples(float2 uv, half strength, float travel, float cameraDistance)
{
    const float2x2 turnA = float2x2(0.8, -0.6, 0.6, 0.8);
    const float2x2 turnB = float2x2(-0.28, -0.96, 0.96, -0.28);
    const float2x2 straight = float2x2(1.0, 0.0, 0.0, 1.0);
    half2 slope = RippleLayer(uv, _RippleSize * 1.7, turnA, travel * 0.8, strength * 0.4);
    // The current draws the ripples out along it, so the surface streaks down the river as it does from the air.
    const float2 drawn = float2(1.0, WATER_RIPPLE_DRAWN);
    slope += RippleLayer(uv * drawn, _RippleSize * 0.45, straight, travel * WATER_RIPPLE_DRAWN, strength * 0.6) * drawn;
    half chop = Fade(cameraDistance, 30.0, 70.0);
    slope += RippleLayer(uv, _RippleSize * 0.12, turnB, travel * 1.2, strength * 0.35 * chop);
    return normalize(half3(slope, 1.0));
}

// Light focused by the ripples onto a shallow bed, as bright wavering threads: two drifting reads of the
// ripple slopes cancel along lines where the light bunches.
half Caustics(float2 bedXZ, half depth)
{
    half shallow = 1.0 - saturate(depth / WATER_CAUSTIC_DEPTH);
    half lines = 0.0;
#ifndef _WATER_NO_CAUSTICS
    [branch] if (shallow > 0.0 && _Caustics > 0.0)
    {
        float drift = _Time.y * 0.15;
        half2 a = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, bedXZ / 1.3 + drift, 0)).xy;
        half2 b = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, bedXZ / 1.9 - drift * 0.7, 0)).xy;
        lines = pow(saturate(1.0 - (abs(a.x + b.x) + abs(a.y + b.y)) * 1.6), 6.0);
    }
#endif

    return lines * shallow * shallow * _Caustics;
}

// Far water is rougher, so it neither mirrors sharply nor aliases.
half DistantSmoothness(float cameraDistance)
{
    return lerp(_Smoothness, _SmoothnessFar, saturate(cameraDistance / max(_SmoothnessFarDistance, 0.001)));
}

// Fills the headroom under _GlintPeak, which sits just past the bloom threshold: a sparkle, never a blow-out.
half3 Glint(Light sun, half3 normalWS, half3 view, half3 base, half smoothness, float cameraDistance)
{
    half fade = Fade(cameraDistance, _GlintFadeStart, _GlintFadeEnd);
    if (fade <= 0.0)
    {
        return half3(0.0, 0.0, 0.0);
    }

    half3 halfway = normalize(sun.direction + view);
    half highlight = pow(saturate(dot(normalWS, halfway)), exp2(smoothness * 11.0));
    half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(view, halfway)), 5.0);
    half3 glint = sun.color * (highlight * fresnel * _GlintStrength * sun.shadowAttenuation * fade);
    half headroom = max(0.0, _GlintPeak - Brightest(base));
    return glint * min(1.0, headroom / max(Brightest(glint), 0.0001));
}

// Clouds dim about two thirds of the scattered sunlight. The refracted bottom already has the cloud shadow.
half3 CloudShade(float3 positionWS)
{
    #if defined(_LIGHT_COOKIES)
        return lerp(half3(1.0, 1.0, 1.0), SampleMainLightCookie(positionWS), 0.65);
    #else
        return half3(1.0, 1.0, 1.0);
    #endif
}
#endif
