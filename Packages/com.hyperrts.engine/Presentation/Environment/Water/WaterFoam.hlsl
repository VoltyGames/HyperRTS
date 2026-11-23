// Water.shader's foam. Each source gives an amount; FoamCover draws the strongest through the foam map's lace, so thick
// foam is whole and thin foam is a few soft flecks. Include after WaterSurface.hlsl and Trample.hlsl.
#ifndef HYPERRTS_WATER_FOAM_INCLUDED
#define HYPERRTS_WATER_FOAM_INCLUDED

#define WATER_FLECK_WIDTH 1.5   // metres across a run of flecks over a ford
#define WATER_FLECK_LENGTH 10.0 // metres along one

// Flecks where a ford's fast water breaks, gathered in runs down the current with open water between.
half FordFoam(float2 uv, float drift, half fast)
{
    [branch] if (fast < 0.001)
    {
        return 0.0;
    }

    half run = Blobs(float2(uv.x / WATER_FLECK_WIDTH, (uv.y - drift * 2.5) / WATER_FLECK_LENGTH));
    return fast * smoothstep(0.35, 0.75, run) * _FordFoam;
}

// A line of foam over the shallowest water: along the banks and round anything standing in it.
half BankFoam(float depth)
{
    return (1.0 - smoothstep(0.0, _BankFoamDepth, depth)) * _BankFoamAmount;
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

// How much of the surface foam covers for an amount, drifting down the current. Foam shows first as soft flecks on
// the densest clumps (G) and fills out from there, thin foam staying see-through; the lace strands (R) only shade it,
// since their straight runs read as sticks when they stand alone.
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
        half edge = 1.0 - amount;
        cover = smoothstep(edge, edge + 0.25, lace.g) * lerp(0.6, 1.0, amount) * (0.75 + 0.25 * lace.r);
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
