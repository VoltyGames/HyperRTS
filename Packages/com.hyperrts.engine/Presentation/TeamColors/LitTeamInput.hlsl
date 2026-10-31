// HyperRTS/LitTeam inputs: the material properties and accessors URP's Lit passes read (laid out like URP's
// LitInput.hlsl, minus detail maps, parallax, the specular workflow and transparency) plus the team colour, its mask,
// its height band, the vertex animation clip and the build progress. Every LitTeam pass includes this, so the SRP
// Batcher sees one UnityPerMaterial layout.
#ifndef HYPERRTS_LIT_TEAM_INPUT_INCLUDED
#define HYPERRTS_LIT_TEAM_INPUT_INCLUDED
// URP's LitForwardPass includes "LitInput.hlsl" itself; its guard keeps this file's declarations the only ones.
#define UNIVERSAL_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/NormalMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/DetailMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/MetallicSpecGloss.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/AlphaBlend.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/OcclusionMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ClearCoat.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Emission.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _EmissionColor;
float4 _TeamColor;
float4 _TeamStrip;
float4 _VATClip;
float4 _VATPositions_TexelSize;
float _VATRowsPerFrame;
float _BuildProgress;
float _BuildDropHeight;
float _BuildDropSlice;
float _BuildDropFrom;
half _LightsOn;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Declared in every variant (only their use is ifdef'd) so constant-buffer offsets match across variants.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _TeamColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _TeamStrip)
    UNITY_DOTS_INSTANCED_PROP(float4, _VATClip)
    UNITY_DOTS_INSTANCED_PROP(float , _BuildProgress)
    UNITY_DOTS_INSTANCED_PROP(float , _LightsOn)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Loaded once per instance into statics, as URP does, so each use doesn't regenerate the load.
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float4 unity_DOTS_Sampled_TeamColor;
static float4 unity_DOTS_Sampled_TeamStrip;
static float4 unity_DOTS_Sampled_VATClip;
static float  unity_DOTS_Sampled_BuildProgress;
static float  unity_DOTS_Sampled_LightsOn;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_OcclusionStrength;

void SetupDOTSLitTeamMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_EmissionColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_TeamColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TeamColor);
    unity_DOTS_Sampled_TeamStrip            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TeamStrip);
    unity_DOTS_Sampled_VATClip              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _VATClip);
    unity_DOTS_Sampled_BuildProgress        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BuildProgress);
    unity_DOTS_Sampled_LightsOn             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _LightsOn);
    unity_DOTS_Sampled_Cutoff               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitTeamMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _EmissionColor          unity_DOTS_Sampled_EmissionColor
#define _TeamColor              unity_DOTS_Sampled_TeamColor
#define _TeamStrip              unity_DOTS_Sampled_TeamStrip
#define _VATClip                unity_DOTS_Sampled_VATClip
#define _BuildProgress          unity_DOTS_Sampled_BuildProgress
#define _LightsOn               unity_DOTS_Sampled_LightsOn
#define _Cutoff                 unity_DOTS_Sampled_Cutoff
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic
#define _BumpScale              unity_DOTS_Sampled_BumpScale
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength

#endif

TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_TeamMask);           SAMPLER(sampler_TeamMask);

#include "Packages/com.hyperrts.engine/Presentation/Rendering/SurfaceMaps.hlsl"

// URP's Utils headers declare these and build every feature on them.
half4 GetEmissionColor()        { return _EmissionColor; }
half GetSmoothness()            { return _Smoothness; }
half GetMetallic()              { return _Metallic; }
half GetOcclusionStrength()     { return _OcclusionStrength; }
float4 GetBaseMapST()           { return _BaseMap_ST; }
float4 GetBaseMapTexelSize()    { return _BaseMap_TexelSize; }

half4 SampleEmissionMap(float2 uv)      { return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv); }
half4 SampleOcclusionMap(float2 uv)     { return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv); }
half4 SampleMetallicGlossMap(float2 uv) { return SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv); }

// There is no specular workflow, but URP's passes still name it behind IsSpecularSetup(), which is always false.
static const half4 _SpecColor = half4(0.0, 0.0, 0.0, 0.0);
half4 GetSpecColor()                    { return _SpecColor; }
half4 SampleSpecGlossMap(float2 uv)     { return _SpecColor; }

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamSurface.hlsl"

#endif
