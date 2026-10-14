// Water.shader's foam, all drawn from the ripple normal map: rapids on fast water, drifting flecks over the whole river
// and a band along the banks. Include after the water's CBUFFER and _NormalMap.
#ifndef HYPERRTS_WATER_FOAM_INCLUDED
#define HYPERRTS_WATER_FOAM_INCLUDED

half FoamSample(float2 uv, float2 tile, float2 dx, float2 dy)
{
    float2 scale = 1.0 / tile;
    return UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv * scale, dx * scale, dy * scale)).x;
}

// Foam only on fast water (vertex G), stretched along the current and broken into patches so the ford doesn't read as a
// striped block. Slow water skips the fetches; gradients taken before the branch keep them valid inside it.
half RapidsFoam(float2 uv, half fast)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    half foam = 0.0;
    [branch] if (fast >= 0.001)
    {
        float drift = _Time.y * _FlowSpeed * 2.5;
        half streak = FoamSample(float2(uv.x, uv.y - drift), float2(_RapidsFoamWidth, _RapidsFoamLength), dx, dy);
        float2 breakupScale = float2(0.37, 1.0) / (_RapidsFoamLength * 1.7);
        float2 breakupUV = float2(uv.x * 0.37 + 0.21, uv.y - drift * 0.6) / (_RapidsFoamLength * 1.7);
        half breakup = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, breakupUV,
            dx * breakupScale, dy * breakupScale)).y;
        foam = fast * smoothstep(0.1, 0.45, streak) * smoothstep(-0.1, 0.35, breakup);
    }

    return foam;
}

// Metres to the waterline: depth over the bed's slope, the slope solved from screen derivatives of depth and position.
// The ribbon's edge lies buried past the bank, so uv.x can't place it. A flat ford bed reads as far from shore.
float ShoreDistance(float depth, float3 positionWS)
{
    float2 ex = ddx(positionWS.xz);
    float2 ey = ddy(positionWS.xz);
    float det = ex.x * ey.y - ex.y * ey.x;
    det += det >= 0.0 ? 1e-6 : -1e-6;
    float dDepthX = ddx(depth);
    float dDepthY = ddy(depth);
    float2 gradient = float2(ey.y * dDepthX - ex.y * dDepthY, ex.x * dDepthY - ey.x * dDepthX) / det;
    float slope = clamp(length(gradient), 0.15, 3.0);
    return depth / slope;
}

// Calm-water foam: sparse streaks drifting at _FlowSpeed, gathered into patches, denser in a wobbling band along the
// banks and a thin line at the waterline. Ground detail Low drops it.
half CalmFoam(float2 uv, float shoreDistance)
{
    #if defined(_TERRAIN_SIMPLE)
        return 0.0;
    #else
        float2 dx = ddx(uv);
        float2 dy = ddy(uv);
        float drift = _Time.y * _FlowSpeed;
        float2 streakTile = float2(1.0, 5.0) * _FlowFoamScale;
        half streak = FoamSample(float2(uv.x, uv.y - drift), streakTile, dx, dy);
        half patches = FoamSample(float2(uv.x + 0.37 * _FlowFoamScale, uv.y - drift * 0.85), streakTile * 6.0, dx, dy);
        half flow = smoothstep(0.22, 0.5, streak) * smoothstep(0.05, 0.35, patches) * _FlowFoamAmount;

        half wobble = FoamSample(float2(0.21, uv.y), float2(1.0, 40.0), float2(0.0, dx.y), float2(0.0, dy.y));
        float bandWidth = _BankFoamWidth * (0.6 + 0.8 * saturate(wobble * 1.5 + 0.5));
        half band = 1.0 - smoothstep(0.0, bandWidth, shoreDistance);
        half broken = smoothstep(0.0, 0.35, streak) * smoothstep(-0.1, 0.3, patches);
        half waterline = 1.0 - smoothstep(0.2, 0.7, shoreDistance);
        half bank = max(band * band, waterline * 0.35) * broken * _BankFoamAmount;
        return max(flow, bank);
    #endif
}

#endif
