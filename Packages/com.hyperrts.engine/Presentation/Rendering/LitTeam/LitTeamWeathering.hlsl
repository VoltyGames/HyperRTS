// Static weathering for buildings: dirt splashed up wall bases, rain streaks down walls, mottled and pooled grime on
// roofs. Procedural in object space, so it needs no textures and every pass sees the same pattern.
#ifndef HYPERRTS_LIT_TEAM_WEATHERING_INCLUDED
#define HYPERRTS_LIT_TEAM_WEATHERING_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/Hash.hlsl"

// Streaks are a few decimetres wide and run a metre or two before breaking; roof patches span a few metres.
#define STREAK_COLUMNS_PER_METRE 2.0
#define STREAK_RUNS_PER_METRE 0.6
// The shader can't see where a wall ends, so streaks grow with height and are whole this far above the ground.
#define STREAK_FULL_HEIGHT 3.0
#define MOTTLE_PER_METRE 0.3

// Weights of the wall (near-vertical) and roof (upward) parts of a surface facing along normalOS.
half WallWeight(float3 normalOS) { return 1.0 - smoothstep(0.2, 0.5, abs(normalOS.y)); }
half RoofWeight(float3 normalOS) { return smoothstep(0.5, 0.9, normalOS.y); }

void ApplyWallWeathering(float3 positionOS, float3 normalOS, half amount, inout SurfaceData surface)
{
    // Across the wall and into it: the depth seeds the noise, so facing walls don't mirror each other.
    float2 facing = normalize(normalOS.xz + float2(1e-5, 0.0));
    float across = dot(positionOS.xz, float2(-facing.y, facing.x));
    float depth = dot(positionOS.xz, facing);
    float height = positionOS.y;

    float coarse = ValueNoise(float2(across, height) * 2.5);
    float ragged = coarse * 0.7 + ValueNoise(float2(across, height + depth) * 7.0) * 0.3;
    half dirt = saturate((1.0 - height / _WeatheringBaseHeight) * 1.8 + (ragged - 0.5) * 0.8 - 0.1);
    dirt *= amount;
    // Mostly the dirt's own colour: it darkens pale concrete and lightens dark paint, so the band reads on both.
    half3 soiled = lerp(surface.albedo * 0.6, _WeatheringDirtColor.rgb, 0.65);
    surface.albedo = lerp(surface.albedo, soiled, dirt);
    surface.smoothness *= 1.0 - dirt * 0.6;

    float column = ValueNoise(float2(across * STREAK_COLUMNS_PER_METRE, depth * 1.7));
    float run = ValueNoise(float2(across * STREAK_COLUMNS_PER_METRE, height * STREAK_RUNS_PER_METRE + depth));
    half streak = smoothstep(0.5, 0.8, column) * (0.4 + 0.6 * run) * saturate(height / STREAK_FULL_HEIGHT);
    streak *= amount * _WeatheringStreaks;
    half3 stained = lerp(surface.albedo * 0.35, _WeatheringDirtColor.rgb * 0.6, 0.3);
    surface.albedo = lerp(surface.albedo, stained, saturate(streak * 2.0));
}

void ApplyRoofWeathering(float3 positionOS, half amount, inout SurfaceData surface)
{
    float2 p = positionOS.xz * MOTTLE_PER_METRE;
    float mottle = ValueNoise(p) * 0.65 + ValueNoise(p * 3.7 + 17.0) * 0.35;
    // Water stands and dirt settles where the mottle is low.
    half pool = smoothstep(0.5, 0.8, 1.0 - mottle) * amount;
    surface.albedo *= 1.0 + (mottle - 0.5) * 0.9 * amount;
    surface.albedo = lerp(surface.albedo, surface.albedo * 0.5 + _WeatheringDirtColor.rgb * 0.15, saturate(pool * 1.5));
    surface.smoothness = saturate(surface.smoothness + (0.5 - mottle) * 0.2 * amount);
}

// teamMask keeps the owner's paint clean where it shows.
void ApplyWeathering(float3 positionOS, float3 normalOS, half teamMask, inout SurfaceData surface)
{
    half amount = _WeatheringStrength * (1.0 - teamMask);
    half wall = WallWeight(normalOS) * amount;
    half roof = RoofWeight(normalOS) * amount * _WeatheringRoofMottle;
    if (wall > 0.0)
    {
        ApplyWallWeathering(positionOS, normalOS, wall, surface);
    }

    if (roof > 0.0)
    {
        ApplyRoofWeathering(positionOS, roof, surface);
    }
}

#endif
