// HyperRTS/Foliage inputs shared by every pass.
#ifndef HYPERRTS_FOLIAGE_INPUT_INCLUDED
#define HYPERRTS_FOLIAGE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Wind.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Trample.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _Smoothness;
    half _Translucency;
    half _HueVariation;
    float _WindStrength;
    float _WindSpeed;
    float _Flutter;
    float _WindScale;
    float _BranchSway;
    float _CardFlutter;
    float _TreeHeight;
    half _GroundBlend;
    half _GroundBlendHeight;
    half _VertexOcclusion;
    float _Trample; // 0 off, 1 grass (flatten), 2 crop (damage)
    half _TwoSidedLeaves;
CBUFFER_END

// PlantShading.hlsl: modelled leaves light the side in view; cards and canopies light both sides alike.
#define PLANT_TWO_SIDED _TwoSidedLeaves

#if defined(_GRASS_INSTANCES)
    #include "Packages/com.hyperrts.engine/Presentation/Environment/Grass/GrassInstance.hlsl"

    // Set per draw by GrassBatch: plants come from _GrassInstances from _GrassOffset by SV_InstanceID.
    StructuredBuffer<GrassInstance> _GrassInstances;
    int _GrassOffset;
    half4 _GrassHealthy;
    half4 _GrassDry;
#endif

float3 PlantTrample(float3 world, float3 origin)
{
    if (_Trample < 0.5)
    {
        return world;
    }

    TrampleSample trample = SampleTrample(origin.xz);
    half amount = _Trample > 1.5 ? trample.crop : trample.flatten;
    return TrampleBend(world, origin, trample.direction, amount);
}

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
#if defined(_TREE_WIND)
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float2 uv4 : TEXCOORD4;
#endif
    half4 color : COLOR;
#if defined(_GRASS_INSTANCES)
    uint instanceID : SV_InstanceID;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#if defined(_GRASS_INSTANCES)
GrassInstance GrassAt(Attributes input)
{
    return _GrassInstances[_GrassOffset + input.instanceID];
}

float2 GrassFacing(GrassInstance grass)
{
    return float2(sin(grass.yaw), cos(grass.yaw));
}
#endif

// Trampled, before wind. Tree wind places nothing: it sways the object-space vertex.
float3 PlaceFoliage(Attributes input, out float3 origin)
{
#if defined(_GRASS_INSTANCES)
    GrassInstance grass = GrassAt(input);
    origin = grass.position;
    float3 scaled = input.positionOS.xyz * float3(grass.width, grass.height, grass.width);
    return PlantTrample(origin + LeanOffset(PlantToWorld(scaled, GrassFacing(grass)), grass.lean), origin);
#elif defined(_TREE_WIND)
    origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    return input.positionOS.xyz;
#else
    origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    return PlantTrample(TransformObjectToWorld(input.positionOS.xyz), origin);
#endif
}

half3 FoliageNormal(Attributes input)
{
#if defined(_GRASS_INSTANCES)
    return normalize(PlantToWorld(input.normalOS, GrassFacing(GrassAt(input))));
#else
    return TransformObjectToWorldNormal(input.normalOS);
#endif
}

// Motion vectors reuse the current object matrix: vegetation never moves.
float3 SwayFoliage(Attributes input, float3 placed, float3 origin, WindFrame frame)
{
#if defined(_TREE_WIND)
    TreeWindSettings settings = TreeWind(_WindStrength, _WindSpeed, _BranchSway, _CardFlutter, _TreeHeight, _WindScale);
    TreeWindVertex v = TreeWindData(placed, input.uv2, input.uv3, input.uv4, input.color);
    return TransformObjectToWorld(TreeWindPosition(v, settings, frame));
#else
    return PlantWindPosition(placed, origin, _WindStrength, _WindSpeed, _Flutter, _WindScale, frame);
#endif
}

float3 FoliagePosition(Attributes input, WindFrame frame, out float3 origin)
{
    float3 placed = PlaceFoliage(input, origin);
    return SwayFoliage(input, placed, origin, frame);
}

#endif
