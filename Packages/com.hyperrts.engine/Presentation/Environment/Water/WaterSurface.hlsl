// Water.shader's surface shading: ripples, caustics, smoothness, glint and cloud shade. Include after the water's
// CBUFFER, _NormalMap and URP's Lighting.hlsl.
#ifndef HYPERRTS_WATER_SURFACE_INCLUDED
#define HYPERRTS_WATER_SURFACE_INCLUDED

#define WATER_FLOW_PERIOD 3.0 // seconds before a drifting ripple layer fades back to its start
#define WATER_CAUSTIC_DEPTH 1.2 // metres of water caustics reach through
#define WATER_BANK_CURRENT 0.35 // share of the current left at the bank

// 1 before start metres, 0 past end.
half Fade(float metres, float start, float end)
{
    return 1.0 - saturate((metres - start) / max(end - start, 0.001));
}

half Brightest(half3 color)
{
    return max(color.r, max(color.g, color.b));
}

// One ripple scale drifting downstream (V) at speed m/s, turned so the scales don't line up. Two phases a
// half period apart fade in turn, so the drift can restart without a jump however the speed varies.
half2 FlowLayer(float2 uv, float size, float2x2 turn, float speed, half strength)
{
    float t0 = frac(_Time.y / WATER_FLOW_PERIOD);
    float t1 = frac(t0 + 0.5);
    float travel = speed * WATER_FLOW_PERIOD;
    float2 a = mul(turn, uv - float2(0.0, t0 * travel)) / size;
    float2 b = mul(turn, uv - float2(0.0, t1 * travel)) / size + 0.5;
    half weight = 1.0 - abs(2.0 * t0 - 1.0);
    half2 slope = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, a), strength).xy * weight
        + UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, b), strength).xy * (1.0 - weight);
    return mul(transpose(turn), slope);
}

// Three scales of the wave spectrum: long swells, ripples and a fine chop, the chop gone by mid range.
half3 Ripples(float2 uv, half strength, float speed, float cameraDistance)
{
    const float2x2 turnA = float2x2(0.8, -0.6, 0.6, 0.8);
    const float2x2 turnB = float2x2(-0.28, -0.96, 0.96, -0.28);
    const float2x2 straight = float2x2(1.0, 0.0, 0.0, 1.0);
    half2 slope = FlowLayer(uv, _RippleSize * 1.7, turnA, speed * 0.8, strength * 0.55);
    slope += FlowLayer(uv, _RippleSize * 0.45, straight, speed, strength * 0.8);
    half chop = Fade(cameraDistance, 30.0, 70.0);
    slope += FlowLayer(uv, _RippleSize * 0.12, turnB, speed * 1.2, strength * 0.45 * chop);
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
