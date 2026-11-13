// HyperRTS/LitTeam interiors: each window's UVs fill one cell of a room-photo atlas, and the view ray is traced into a
// box room behind the glass and projected back into the photo. Emission alpha holds the photo's back-wall share.
#ifndef HYPERRTS_LIT_TEAM_INTERIOR_INCLUDED
#define HYPERRTS_LIT_TEAM_INTERIOR_INCLUDED

// Where a ray entering the window at p (0..1) meets the room, as a point in its photo.
float2 InteriorRoomPoint(float2 p, float3 ray, half backWall)
{
    if (backWall > 0.98h)
    {
        return p;
    }

    float3 dir = float3(ray.xy, max(-ray.z, 1e-4));
    float2 safe = float2(abs(dir.x) < 1e-5 ? 1e-5 : dir.x, abs(dir.y) < 1e-5 ? 1e-5 : dir.y);
    float2 toWall = (step(0.0, dir.xy) - p) / safe;
    float t = min(min(toWall.x, toWall.y), 1.0 / dir.z);
    float3 hit = float3(p, 0.0) + dir * t;
    // Camera distance at which a back wall one window deep fills backWall of the frame.
    float camera = backWall / (1.0 - backWall);
    return 0.5 + (hit.xy - 0.5) * camera / (camera + hit.z);
}

// Room light through the glass, less what Fresnel reflects.
half3 InteriorEmission(float2 uv, float3 viewDirTS)
{
    float2 grid = max(_InteriorAtlas.xy, 1.0);
    float2 cellPoint = uv * grid;
    float2 cell = floor(cellPoint);
    half backWall = SAMPLE_TEXTURE2D_LOD(_EmissionMap, sampler_EmissionMap, (cell + 0.5) / grid, 0).a;
    float inset = _InteriorAtlas.z;
    float2 window = saturate((cellPoint - cell - inset) / (1.0 - 2.0 * inset));
    float2 room = inset + InteriorRoomPoint(window, -viewDirTS, backWall) * (1.0 - 2.0 * inset);
    // The window's own UV gradients keep the mip steady at room corners.
    half3 photo = SAMPLE_TEXTURE2D_GRAD(_EmissionMap, sampler_EmissionMap, (cell + room) / grid, ddx(uv), ddy(uv)).rgb;

    half facing = saturate(viewDirTS.z);
    half fresnel = 0.04h + 0.96h * pow(1.0h - facing, 5.0h);
    return photo * _EmissionColor.rgb * _LightsOn * (1.0h - fresnel);
}

#endif
