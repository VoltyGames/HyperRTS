// Water.shader's foam. Each source gives an amount; FoamCover draws the strongest through the foam map's lace, so thick
// foam is solid and thin foam breaks into strands and flecks. Include after the water's CBUFFER and Trample.hlsl.
#ifndef HYPERRTS_WATER_FOAM_INCLUDED
#define HYPERRTS_WATER_FOAM_INCLUDED

#define WATER_GUST_DIRECTION float2(0.8, 0.6) // the wind the ripple spectrum was made for

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

// A broken line at the waterline, a looser band behind it and swells running in to the bank.
half BankFoam(float2 uv, float shoreDistance)
{
    half amount = 0.0;
    [branch] if (shoreDistance < _BankFoamWidth * 1.4)
    {
        half wander = Blobs(float2(0.21, uv.y / 40.0));
        float bandWidth = _BankFoamWidth * (0.6 + 0.8 * wander);
        half band = 1.0 - smoothstep(0.0, bandWidth, shoreDistance);
        half waterline = 1.0 - smoothstep(0.1, 0.6, shoreDistance);
        // The phase falls with distance, so crests travel shoreward; the wander bends them off straight lines.
        float phase = shoreDistance / _ShoreWaveLength + _Time.y / _ShoreWavePeriod + wander * 0.6;
        half crest = pow(saturate(sin(phase * TWO_PI) * 0.5 + 0.5), 6.0) * band;
        amount = max(max(waterline * 0.5, band * band * 0.3) * _BankFoamAmount, crest * _ShoreWaveFoam);
    }

    return amount;
}

// Foam round each bridge pier, which the water's mesh frame orients: down is its V on the water plane, across its U.
// x: a thin cushion upstream and a short wake that widens and thins out by _PierWakeLength. y: choppy water round it,
// twice as long.
half2 PierWake(float3 positionWS, float2 down, float2 across)
{
    half foam = 0.0;
    half chop = 0.0;
    for (int i = 0; i < min((int)_HyperRTSWaterPierCount, MAX_WATER_PIERS); i++)
    {
        float2 offset = positionWS.xz - _HyperRTSWaterPiers[i].xy;
        float halfWidth = _HyperRTSWaterPiers[i].z;
        float u = abs(dot(offset, across));
        float v = dot(offset, down);

        float rim = length(float2(u, min(v, 0.0))) - halfWidth;
        half cushion = (1.0 - smoothstep(0.0, _PierCushionWidth, rim)) * step(0.0, rim) * step(v, 0.0);

        float spread = halfWidth * 1.3 + max(v, 0.0) * 0.18;
        half behind = step(0.0, v);
        half along = 1.0 - smoothstep(0.0, _PierWakeLength, v);
        half wake = (1.0 - smoothstep(0.4, 1.0, u / spread)) * along * behind;
        half rough = (1.0 - smoothstep(0.5, 1.0, u / (spread * 1.6))) * (1.0 - smoothstep(0.0, _PierWakeLength * 2.0, v)) * behind;

        foam = max(foam, max(cushion * 0.85, wake * 0.9));
        chop = max(chop, rough);
    }

    return half2(foam * _PierWakeFoam, chop * _PierChop);
}

// Foam lines where slicks meet rougher water, which gathers the surface film, plus the trails below fords (vertex R).
half FlowFoam(half slick, half trail)
{
    half seam = 1.0 - smoothstep(0.0, 0.08, abs(slick - 0.62));
    return max(seam * _FlowFoamAmount, trail * 0.7);
}

// How much of the surface foam covers for an amount: thick foam is whole, thinner foam keeps only the clumps (G) and
// then the lace strands (R) inside them, drifting down the current.
half FoamCover(half amount, float2 uv, float drift)
{
    // Gradients taken before the branch keep the fetch valid inside it.
    float2 scale = 1.0 / float2(_FoamSize, _FoamSize * 1.6);
    float2 dx = ddx(uv) * scale;
    float2 dy = ddy(uv) * scale;
    half cover = 0.0;
    [branch] if (amount > 0.01)
    {
        half3 lace = SAMPLE_TEXTURE2D_GRAD(_FoamMap, sampler_FoamMap, float2(uv.x, uv.y - drift) * scale, dx, dy).rgb;
        half prone = lace.g * 0.6 + lace.r * 0.4;
        half edge = 1.0 - amount;
        cover = smoothstep(edge - 0.05, edge + 0.05, prone) * (0.7 + 0.3 * lace.r);
    }

    return cover;
}

struct WakeSample
{
    half foam;
    half2 slope; // wake per metre in x and z, read as a height to tilt the surface
};

// Foam where units moved through the water. Water with no wake skips the slope reads.
WakeSample SampleWakeFoam(float3 positionWS)
{
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
        wake.foam = saturate(amount * 1.4) * _WakeFoam;
    }

    return wake;
}

#endif
