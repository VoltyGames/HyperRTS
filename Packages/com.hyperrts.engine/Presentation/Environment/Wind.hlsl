// Vegetation wind from the globals the scene's Wind publishes. Include after URP's Core.hlsl.
// Trees (TreeWindPosition) carry wind data per vertex, Unity object space in metres:
//   TEXCOORD2.xy branch pivot xy, TEXCOORD3.x branch pivot z, TEXCOORD3.y card pivot x, TEXCOORD4.xy card pivot yz
//   COLOR g branch flex (0 at the pivot, 1 at the tip), b stiffness (0 limb, 1 twig), a flutter weight (1 on cards)
// Plants without wind data (PlantWindPosition) sway about their origin with height.
// Every function takes a WindFrame, so motion vectors can rebuild last frame's position.
#ifndef HYPERRTS_WIND_INCLUDED
#define HYPERRTS_WIND_INCLUDED

#define WIND_TAU 6.2831853

float4 _HyperRTSWindDirection;  // xz unit downwind
float _HyperRTSWindStrength;
float4 _HyperRTSWindGust;       // x gust amount, y turbulence, z gust tiles per metre
float4 _HyperRTSWindGustOffset; // xy this frame's gust scroll (tiles), zw last frame's
float _HyperRTSWindDetail;      // 0 trunk, 1 + branches, 2 + flutter
TEXTURE2D(_HyperRTSWindGustMap);
SAMPLER(sampler_HyperRTSWindGustMap);

struct WindFrame
{
    float time;
    float2 gustOffset;
};

struct TreeWindVertex
{
    float3 positionOS;
    float3 branchPivot;
    float3 cardPivot;
    float flex;
    float stiffness;
    float flutter;
};

// Per-material tuning; bark and leaves of one tree must share it.
struct TreeWindSettings
{
    float trunkSway;   // tip travel per metre of height at full push
    float speed;       // sway frequency
    float branchSway;  // radians at the tip of a branch at full push
    float cardFlutter; // radians a leaf card turns
    float height;      // object-space metres from the origin to the top
    float scale;       // 0 = no wind (fallen trees)
};

WindFrame WindThisFrame()
{
    WindFrame frame;
    frame.time = _TimeParameters.x;
    frame.gustOffset = _HyperRTSWindGustOffset.xy;
    return frame;
}

WindFrame WindLastFrame()
{
    WindFrame frame;
    frame.time = _LastTimeParameters.x;
    frame.gustOffset = _HyperRTSWindGustOffset.zw;
    return frame;
}

TreeWindVertex TreeWindData(float3 positionOS, float2 uv2, float2 uv3, float2 uv4, half4 color)
{
    TreeWindVertex v;
    v.positionOS = positionOS;
    v.branchPivot = float3(uv2.xy, uv3.x);
    v.cardPivot = float3(uv3.y, uv4.xy);
    v.flex = color.g;
    v.stiffness = color.b;
    v.flutter = color.a;
    return v;
}

float WindHash(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float3 WindSafeNormalize(float3 v)
{
    return v * rsqrt(max(dot(v, v), 1e-8));
}

// The steady strength at a world point, swelled or eased by the gust rolling over it.
float WindPush(float2 positionXZ, WindFrame frame)
{
    float2 uv = positionXZ * _HyperRTSWindGust.z - frame.gustOffset;
    float gust = SAMPLE_TEXTURE2D_LOD(_HyperRTSWindGustMap, sampler_HyperRTSWindGustMap, uv, 0).r;
    return _HyperRTSWindStrength * (1.0 + _HyperRTSWindGust.x * (gust * 2.0 - 1.0));
}

// Back-and-forth on top of the push, as large as the turbulence.
float WindSwing(float time, float phase)
{
    float wave = sin(time + phase) * 0.6 + sin(time * 2.3 + phase + 1.7) * 0.4;
    return wave * _HyperRTSWindGust.y * _HyperRTSWindStrength;
}

float3 WindRotate(float3 p, float3 pivot, float3 axis, float angle)
{
    float3 v = p - pivot;
    float s, c;
    sincos(angle, s, c);
    return pivot + v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

float3 CardFlutter(float3 p, TreeWindVertex v, TreeWindSettings s, float push, WindFrame frame, float3 origin)
{
    float3 seed = v.cardPivot + origin;
    float3 axis = WindSafeNormalize(float3(WindHash(seed) - 0.5, 0.3, WindHash(seed.zxy) - 0.5));
    float frequency = s.speed * lerp(6.0, 10.0, v.stiffness);
    float wave = sin(frame.time * frequency + WindHash(seed.yzx) * WIND_TAU);
    float angle = v.flutter * s.cardFlutter * saturate(push * 2.0) * wave;
    return WindRotate(p, v.cardPivot, axis, angle);
}

float3 BranchSway(float3 p, TreeWindVertex v, TreeWindSettings s, float3 downwind, WindFrame frame)
{
    float3 pivotWS = TransformObjectToWorld(v.branchPivot);
    float push = WindPush(pivotWS.xz, frame) * s.scale;
    float frequency = s.speed * lerp(1.0, 3.0, v.stiffness);
    float swing = WindSwing(frame.time * frequency, WindHash(pivotWS) * WIND_TAU) * s.scale;
    float amplitude = s.branchSway * lerp(1.0, 0.5, v.stiffness);
    float3 axis = WindSafeNormalize(cross(float3(0.0, 1.0, 0.0), downwind));
    return WindRotate(p, v.branchPivot, axis, v.flex * amplitude * (push + swing));
}

// Leans the tree downwind on an (h / H)^2 curve, then pulls each point back to its distance from the root so the
// trunk keeps its length.
float3 TrunkBend(float3 p, TreeWindSettings s, float3 downwind, float push, WindFrame frame, float3 origin)
{
    float swing = WindSwing(frame.time * s.speed * 0.6, WindHash(origin) * WIND_TAU) * s.scale;
    float along = max(p.y, 0.0) / max(s.height, 0.01);
    float3 bent = p + downwind * ((push + swing) * s.trunkSway * s.height * along * along);
    float reach = length(p);
    return WindSafeNormalize(bent) * reach;
}

// Object-space position of a tree vertex under this frame's wind: card flutter, branch sway, then trunk bend.
float3 TreeWindPosition(TreeWindVertex v, TreeWindSettings s, WindFrame frame)
{
    float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float3 downwindWS = float3(_HyperRTSWindDirection.x, 0.0, _HyperRTSWindDirection.z);
    float3 downwind = WindSafeNormalize(TransformWorldToObjectDir(downwindWS, false));
    float push = WindPush(origin.xz, frame) * s.scale;

    float3 p = v.positionOS;
    UNITY_BRANCH
    if (_HyperRTSWindDetail >= 2.0)
    {
        p = CardFlutter(p, v, s, push, frame, origin);
    }
    UNITY_BRANCH
    if (_HyperRTSWindDetail >= 1.0)
    {
        p = BranchSway(p, v, s, downwind, frame);
    }
    return TrunkBend(p, s, downwind, push, frame, origin);
}

// World position of a plant vertex (no wind data): sways about the origin with height, leaves flutter on top.
float3 PlantWindPosition(float3 world, float3 origin, float sway, float speed, float flutter, float scale, WindFrame frame)
{
    float height = max(0.0, world.y - origin.y);
    float push = WindPush(origin.xz, frame) * scale;
    float swing = WindSwing(frame.time * speed, dot(origin.xz, float2(0.11, 0.07))) * scale;
    float3 moved = world;
    moved.xz += _HyperRTSWindDirection.xz * ((push + swing) * sway * height);

    UNITY_BRANCH
    if (_HyperRTSWindDetail >= 2.0)
    {
        float wave = sin(frame.time * 7.0 + dot(world, float3(1.9, 2.3, 1.7)));
        moved += float3(wave, wave * 0.5, -wave) * (flutter * saturate(height * 0.5) * saturate(push * 2.0));
    }
    return moved;
}

#endif
