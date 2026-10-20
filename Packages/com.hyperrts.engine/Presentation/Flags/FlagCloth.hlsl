// HyperRTS/Flag's inputs and cloth motion, shared by every pass so the SRP Batcher sees one UnityPerMaterial layout.
#ifndef HYPERRTS_FLAG_CLOTH_INCLUDED
#define HYPERRTS_FLAG_CLOTH_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/WindGlobals.hlsl"

TEXTURE2D_ARRAY(_FlagArray);
SAMPLER(sampler_FlagArray);

CBUFFER_START(UnityPerMaterial)
    float4 _FlagState;
    float4 _FlagColor;
    half _BandWidth;
    half _Translucency;
    float _WaveLength;
    float _WaveSpeed;
    float _WaveAmplitude;
CBUFFER_END

// Mirrors URP 17.6 UnlitInput.hlsl: FlagState and FlagColor come per entity under Entities Graphics.
#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _FlagState)
    UNITY_DOTS_INSTANCED_PROP(float4, _FlagColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_FlagState;
static float4 unity_DOTS_Sampled_FlagColor;

void SetupDOTSFlagMaterialPropertyCaches()
{
    unity_DOTS_Sampled_FlagState = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FlagState);
    unity_DOTS_Sampled_FlagColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FlagColor);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSFlagMaterialPropertyCaches()

#define _FlagState unity_DOTS_Sampled_FlagState
#define _FlagColor unity_DOTS_Sampled_FlagColor
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Cloth
{
    float3 positionWS;
    float3 normalWS;
};

// In still air the flag keeps the heading it was placed with.
float3 Downwind()
{
    float3 placed = TransformObjectToWorldDir(float3(1.0, 0.0, 0.0));
    float2 wind = _HyperRTSWindDirection.xz;
    float2 heading = dot(wind, wind) > 1e-6 ? wind : placed.xz;
    heading = normalize(heading + 1e-5);
    return float3(heading.x, 0.0, heading.y);
}

// Streams downwind from the pole top, sliding down as it is lowered and shrinking to nothing while hidden.
Cloth FlagCloth(float3 positionOS, float2 uv)
{
    float4x4 objectToWorld = GetObjectToWorldMatrix();
    float3 origin = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);
    float height = length(float3(objectToWorld._m01, objectToWorld._m11, objectToWorld._m21)) * _FlagState.z;
    float strength = saturate(_HyperRTSWindStrength);

    float along = positionOS.x;
    float k = TWO_PI / _WaveLength;
    float phase = along * k - _Time.y * _WaveSpeed * (0.6 + strength) + dot(origin.xz, float2(0.37, 0.23));
    float amplitude = _WaveAmplitude * (0.5 + strength) * uv.x;
    float ripple = amplitude * sin(phase);
    float slope = amplitude * k * cos(phase);
    float sag = (1.0 - strength) * 0.25 * uv.x * uv.x;
    float drop = (1.0 - _FlagState.y) * _FlagState.w;

    float3 downwind = Downwind();
    float3 side = float3(-downwind.z, 0.0, downwind.x);
    float3 offset = downwind * along + side * ripple + float3(0.0, positionOS.y - drop - sag, 0.0);

    Cloth cloth;
    cloth.positionWS = origin + offset * height;
    cloth.normalWS = normalize(cross(float3(0.0, 1.0, 0.0), normalize(downwind + side * slope)));
    return cloth;
}

#endif
