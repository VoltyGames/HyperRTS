// The road pass: ribbon vertices that follow craters, and a fragment that lays scorch, stains and rain over the
// road surface and lights it like the terrain.
#ifndef HYPERRTS_ROAD_FORWARD_INCLUDED
#define HYPERRTS_ROAD_FORWARD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/ShaderLibrary/SceneLighting.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainSurfaceData.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Weather/Wetness.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Craters/CraterSink.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Terrain/TerrainCraterGround.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Scatter/BuildingFootprints.hlsl"

// A bowl this deep breaks the road: it is gone by the far depth.
#define ROAD_BREAK_NEAR 0.2
#define ROAD_BREAK_FAR 0.4

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half opacity : TEXCOORD4;
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        half3 vertexLighting : TEXCOORD5;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings Vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    // The ribbon spans many craters, so each vertex follows the ground at its own spot.
    float3 positionWS = position.positionWS;
    positionWS.y -= CraterDepth(positionWS.xz);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.uv = input.uv;
    output.normalWS = normals.normalWS;
    output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.opacity = input.color.a;
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        output.vertexLighting = VertexLighting(position.positionWS, normals.normalWS);
    #endif
    return output;
}

// Noise decides coverage toward the sides, so gravel thins in patches instead of along a clean line. A road
// never draws where a building stands, so it can't show through a yard, a pit or a silo.
half Coverage(Varyings input, half albedoAlpha, float4 tracks)
{
    half unbroken = 1.0 - smoothstep(ROAD_BREAK_NEAR, ROAD_BREAK_FAR, CraterDepth(input.positionWS.xz, tracks));
    half yard = 1.0 - smoothstep(0.3, 0.7, BuildingCover(input.positionWS.xz));
    half inside = 1.0 - abs(input.uv.x * 2.0 - 1.0);
    half fade = saturate(inside / _EdgeWidth);
    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz / _NoiseSize).r;
    half crumble = saturate(fade * (1.0 + _EdgeNoise) - noise * _EdgeNoise);
    return smoothstep(0.0, 1.0, crumble) * albedoAlpha * input.opacity * yard * unbroken;
}

// How readily water stands here: in the wheel ruts and along the edges, broken up by world noise.
// Explicit gradients (texture detail level), as it runs inside a branch.
half PuddleCollect(Varyings input, float2 st, float2 dx, float2 dy)
{
    half fromMiddle = abs(input.uv.x - 0.5);
    half rut = _RutPuddles * (1.0 - smoothstep(0.0, _RutWidth, abs(fromMiddle - _RutOffset)));
    half edge = _EdgePuddles * saturate((fromMiddle - 0.35) / 0.15);
    half noise = SAMPLE_TEXTURE2D_GRAD(_NoiseMap, sampler_NoiseMap, st, dx, dy).r;
    return max(rut, edge) + (noise - 0.5) * 1.2;
}

// The map's top-down ground colour (GroundColor); the rect is zero where a map has none.
TEXTURE2D(_HyperRTSGroundColor);
SAMPLER(sampler_HyperRTSGroundColor);
float4 _HyperRTSGroundRect;

// The broad patches span this many edge noise tiles, so they read as wear, not grain.
#define ROAD_PATCH_TILES 5.0

// Breaks the tile's repeat with broad light and dark patches, and lets the worn edge take the colour of the ground
// it frays into, so the road sits in the map instead of on it.
half3 RoadAlbedo(half3 albedo, Varyings input)
{
    float2 patchUV = input.positionWS.xz / (_NoiseSize * ROAD_PATCH_TILES);
    half patch = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, patchUV).r;
    albedo *= 1.0 + (patch - 0.5) * 2.0 * _PatchShade;
    if (_HyperRTSGroundRect.z <= 0.0)
    {
        return albedo;
    }

    half inside = 1.0 - abs(input.uv.x * 2.0 - 1.0);
    half edge = 1.0 - saturate(inside / (_EdgeWidth * 2.0));
    float2 groundUV = RectUV(_HyperRTSGroundRect, input.positionWS.xz);
    half3 ground = SAMPLE_TEXTURE2D(_HyperRTSGroundColor, sampler_HyperRTSGroundColor, groundUV).rgb;
    return lerp(albedo, ground, edge * _EdgeGround);
}

half3x3 RoadTangentToWorld(Varyings input)
{
    half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    return half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
}

void ApplyRain(inout TerrainSurface surface, Varyings input)
{
    float2 st = input.positionWS.xz / _PuddleNoiseSize;
    float2 dx = ddx(st);
    float2 dy = ddy(st);
    UNITY_BRANCH
    if (!RainWet())
    {
        return;
    }

    Wet(surface.albedo, surface.smoothness, surface.normalTS, _HyperRTSWetness, _WetDarkening, _WetSmoothness);
    half collect = PuddleCollect(input, st, dx, dy);
    RainPuddles(surface.albedo, surface.smoothness, surface.normalTS, collect, _PuddleCoverage, _PuddleSmoothness);
}

// A hard road holds less ash than soil, so it still reads as a road through a burn.
#define ROAD_ASH 0.45

// The terrain's scorch and stains, over the road's own surface.
void ApplyScars(inout TerrainSurface surface, Varyings input, half3x3 tangentToWorld, float4 tracks)
{
    TerrainPixel pixel = (TerrainPixel)0;
    pixel.positionWS = input.positionWS;
    pixel.xz = input.positionWS.xz;
    pixel.dx = ddx(pixel.xz);
    pixel.dy = ddy(pixel.xz);
    pixel.dPdx = ddx(input.positionWS);
    pixel.dPdy = ddy(input.positionWS);
    pixel.frame = tangentToWorld;

    ApplyScorch(surface, tracks.b);
    ApplyStains(surface, pixel, ROAD_ASH);
    ApplyCraterGround(surface, pixel, GroundSunk(tracks.a), tracks.b);
}

InputData Inputs(Varyings input, half3x3 tangentToWorld, half3 normalTS)
{
    InputData data = GroundInputData(input.positionWS, input.positionCS, tangentToWorld, normalTS);
    const bool isSurfaceTypeTransparent = false;
    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS, isSurfaceTypeTransparent, data.geometricNormalWS);
    data.bakedGI = SurfaceCacheAmbient(input.positionWS, data.normalWS, data.viewDirectionWS, input.positionCS.xy);
    data.shadowMask = half4(1.0, 1.0, 1.0, 1.0);
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        data.vertexLighting = input.vertexLighting;
    #endif
    return data;
}

half4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float2 uv = float2(input.uv.x, input.uv.y / _TileLength);
    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _Tint;
    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
    float4 tracks = SampleTracks(input.positionWS.xz);

    TerrainSurface surface = (TerrainSurface)0;
    surface.albedo = RoadAlbedo(albedo.rgb, input);
    surface.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
    surface.occlusion = mask.g;
    surface.smoothness = mask.a * _Smoothness;
    half3x3 tangentToWorld = RoadTangentToWorld(input);
    ApplyScars(surface, input, tangentToWorld, tracks);
    ApplyRain(surface, input);

    InputData inputData = Inputs(input, tangentToWorld, surface.normalTS);
    SurfaceData surfaceData = TerrainSurfaceData(surface);
    #if defined(_DBUFFER)
        const bool isSpecularSetup = false;
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData, isSpecularSetup);
    #endif
    half4 color = GroundFragmentPBR(inputData, surfaceData);
    return half4(color.rgb, Coverage(input, albedo.a, tracks));
}

#endif
