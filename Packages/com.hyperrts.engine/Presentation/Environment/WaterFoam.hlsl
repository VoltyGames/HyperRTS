// Water.shader's foam, all from the ripple normal map. Include after the water's CBUFFER, _NormalMap and Trample.hlsl.
#ifndef HYPERRTS_WATER_FOAM_INCLUDED
#define HYPERRTS_WATER_FOAM_INCLUDED

half2 FoamSample(float2 uv, float2 tile, float2 dx, float2 dy)
{
    float2 scale = 1.0 / tile;
    return UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv * scale, dx * scale, dy * scale)).xy;
}

// Fast water (vertex G), broken into patches so the ford doesn't read as a striped block.
// Gradients taken before the branch keep the fetches valid inside it.
half RapidsFoam(float2 uv, half fast)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    half foam = 0.0;
    [branch] if (fast >= 0.001)
    {
        float drift = _Time.y * _FlowSpeed * 2.5;
        half streak = FoamSample(float2(uv.x, uv.y - drift), float2(_RapidsFoamWidth, _RapidsFoamLength), dx, dy).x;
        float2 squeeze = float2(0.37, 1.0);
        half breakup = FoamSample(float2(uv.x * 0.37 + 0.21, uv.y - drift * 0.6), _RapidsFoamLength * 1.7,
            dx * squeeze, dy * squeeze).y;
        foam = fast * smoothstep(0.1, 0.45, streak) * smoothstep(-0.1, 0.35, breakup);
    }

    return foam;
}

// Metres to the waterline from depth and screen derivatives: the ribbon's edge lies buried past the bank, so uv.x
// can't place it. A flat ford bed reads as far from shore.
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

// Swells only pull the waterline down: the bank hides water above it, so rising would harden the soft edge.
float ShoreLap(float2 uv)
{
    return -(0.5 + 0.5 * sin((_Time.y / _ShoreWavePeriod + uv.y * 0.07) * TWO_PI)) * _ShoreLap * _ShoreFadeDepth;
}

// Calm-water foam: drifting streaks in the trails below crossings (vertex R), and bands along the banks. Ground
// detail Low drops it.
half CalmFoam(float2 uv, float shoreDistance, half trail)
{
    #if defined(_TERRAIN_SIMPLE)
        return 0.0;
    #else
        float2 dx = ddx(uv);
        float2 dy = ddy(uv);
        float drift = _Time.y * _FlowSpeed;
        float2 streakTile = float2(1.0, 5.0) * _FlowFoamScale;
        half streak = FoamSample(float2(uv.x, uv.y - drift), streakTile, dx, dy).x;
        half patches = FoamSample(float2(uv.x + 0.37 * _FlowFoamScale, uv.y - drift * 0.85), streakTile * 6.0, dx, dy).x;
        half flow = smoothstep(0.22, 0.5, streak) * smoothstep(0.05, 0.35, patches) * _FlowFoamAmount * trail;

        // The band is at most 1.4 widths wide and the waterline 0.7 m: open water skips the edgeWander fetch.
        half bank = 0.0;
        [branch] if (shoreDistance < max(_BankFoamWidth * 1.4, 0.7))
        {
            half edgeWander = FoamSample(float2(0.21, uv.y), float2(1.0, 40.0), float2(0.0, dx.y), float2(0.0, dy.y)).x;
            float bandWidth = _BankFoamWidth * (0.6 + 0.8 * saturate(edgeWander * 1.5 + 0.5));
            half band = 1.0 - smoothstep(0.0, bandWidth, shoreDistance);
            half broken = smoothstep(0.0, 0.35, streak) * smoothstep(-0.1, 0.3, patches);
            half waterline = 1.0 - smoothstep(0.2, 0.7, shoreDistance);

            // The phase falls with distance, so crests travel shoreward; the edgeWander bends them off straight lines.
            float phase = shoreDistance / _ShoreWaveLength + _Time.y / _ShoreWavePeriod + edgeWander * 0.6;
            half crest = pow(saturate(sin(phase * TWO_PI) * 0.5 + 0.5), 6.0) * band;
            bank = max(max(band * band, waterline * 0.35) * _BankFoamAmount, crest * _ShoreWaveFoam) * broken;
        }

        return max(flow, bank);
    #endif
}

struct WakeSample
{
    half foam;
    half2 slope; // wake per metre in x and z, read as a height to tilt the surface
};

// Foam where units moved through the water, broken up at its fading edges. Water with no wake skips the slope reads.
WakeSample SampleWakeFoam(float3 positionWS, float2 uv)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    WakeSample wake;
    wake.foam = 0.0;
    wake.slope = half2(0.0, 0.0);
    half amount = SampleWake(positionWS);
    [branch] if (amount > 0.002)
    {
        const float spacing = 0.6; // metres between slope reads, about a wake texel
        half aheadX = SampleWake(positionWS + float3(spacing, 0.0, 0.0));
        half aheadZ = SampleWake(positionWS + float3(0.0, 0.0, spacing));
        wake.slope = (half2(aheadX, aheadZ) - amount) / spacing;
        half breakup = FoamSample(float2(uv.x, uv.y - _Time.y * _FlowSpeed), _FlowFoamScale * 0.5, dx, dy).x;
        wake.foam = smoothstep(0.15, 0.6, amount * (0.65 + 0.7 * saturate(breakup + 0.5))) * _WakeFoam;
    }

    return wake;
}

#endif
