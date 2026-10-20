// HyperRTS/Terrain inputs: material properties, layer textures, and what the terrain sets on every draw
// (instanced heightmap patches, holes).
#ifndef HYPERRTS_TERRAIN_INPUT_INCLUDED
#define HYPERRTS_TERRAIN_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Control0_TexelSize;
    float _LayerCount;
    float _HeightBlendDepth;
    float _FarTiling;
    float _FarStart;
    float _FarEnd;
    half _FarBlend;
    float _VariationSize;
    half _Variation;
    half _GrassPatches;
    half _WetDarkening;
    half _WetSmoothness;
    half _RainDarkening;
    half _RainSmoothness;
    half _GrassWetness;
    half _PuddleCoverage;
    half _PuddleSmoothness;
    float _PuddleNoiseSize;
CBUFFER_END

CBUFFER_START(_Terrain)
    float4 _TerrainHeightmapRecipSize; // (1 / width, 1 / height, 1 / (width - 1), 1 / (height - 1))
    float4 _TerrainHeightmapScale;     // (scale x, scale y / max height, scale z, 0)
    #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
    #endif
CBUFFER_END

TEXTURE2D_ARRAY(_AlbedoArray);
SAMPLER(sampler_AlbedoArray);
TEXTURE2D_ARRAY(_NormalArray);
TEXTURE2D_ARRAY(_MaskArray);
TEXTURE2D(_LayerParams);
TEXTURE2D(_Control0);
TEXTURE2D(_Control1);
TEXTURE2D(_Control2);
TEXTURE2D(_Control3);
TEXTURE2D(_MacroMap);
TEXTURE2D(_RowMap);
TEXTURE2D(_NoiseMap);
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

#ifdef UNITY_INSTANCING_ENABLED
    TYPED_TEXTURE2D(float4, _TerrainHeightmapTexture);
    TEXTURE2D(_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData) // (x base, y base, skip scale, -)
UNITY_INSTANCING_BUFFER_END(Terrain)

struct TerrainAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Instanced draws send flat patches: position, normal and terrain UV come from the heightmap.
void TerrainInstancing(inout TerrainAttributes input)
{
    #ifdef UNITY_INSTANCING_ENABLED
        float4 patch = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
        float2 coords = (input.positionOS.xy + patch.xy) * patch.z;
        float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(coords, 0)));
        input.positionOS.xz = coords * _TerrainHeightmapScale.xz;
        input.positionOS.y = height * _TerrainHeightmapScale.y;
        input.normalOS = _TerrainNormalmapTexture.Load(int3(coords, 0)).rgb * 2.0 - 1.0;
        input.uv = coords * _TerrainHeightmapRecipSize.zw;
    #endif
}

// Instanced patches read the terrain's normal map per pixel, so shading doesn't change with the patch LOD.
half3 TerrainNormalWS(float2 uv, half3 vertexNormalWS)
{
    #ifdef UNITY_INSTANCING_ENABLED
        float2 coords = (uv / _TerrainHeightmapRecipSize.zw + 0.5) * _TerrainHeightmapRecipSize.xy;
        half3 normalOS = SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_LinearClamp, coords).rgb * 2.0 - 1.0;
        return TransformObjectToWorldNormal(normalOS);
    #else
        return normalize(vertexNormalWS);
    #endif
}

void ClipHoles(float2 uv)
{
    #ifdef _ALPHATEST_ON
        // Compressed hole maps store 0 as about 1/2047.
        clip(SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r - 0.0005);
    #endif
}

// Alphamap texel centres sit on the terrain's edges, like its heightmap samples.
float2 ControlUV(float2 uv)
{
    return (uv * (_Control0_TexelSize.zw - 1.0) + 0.5) * _Control0_TexelSize.xy;
}

#endif
