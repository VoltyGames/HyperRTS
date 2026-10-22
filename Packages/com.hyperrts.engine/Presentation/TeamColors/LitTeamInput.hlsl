// HyperRTS/LitTeam inputs: URP Lit's material properties and accessors (laid out like URP's LitInput.hlsl) plus the
// team colour, its mask, its height band, the vertex animation clip and the build progress. Every LitTeam pass includes this, so the SRP
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
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ParallaxMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/MetallicSpecGloss.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/AlphaBlend.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/OcclusionMap.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/ClearCoat.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Emission.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _DetailAlbedoMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
float4 _TeamColor;
float4 _TeamStrip;
float4 _VATClip;
float4 _VATPositions_TexelSize;
float _VATRowsPerFrame;
float _BuildProgress;
half _LightsOn;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Declared in every variant (only their use is ifdef'd) so constant-buffer offsets match across variants.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
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
    UNITY_DOTS_INSTANCED_PROP(float , _Parallax)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatMask)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailAlbedoMapScale)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailNormalMapScale)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Loaded once per instance into statics, as URP does, so each use doesn't regenerate the load.
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
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
static float  unity_DOTS_Sampled_Parallax;
static float  unity_DOTS_Sampled_OcclusionStrength;
static float  unity_DOTS_Sampled_ClearCoatMask;
static float  unity_DOTS_Sampled_ClearCoatSmoothness;
static float  unity_DOTS_Sampled_DetailAlbedoMapScale;
static float  unity_DOTS_Sampled_DetailNormalMapScale;

void SetupDOTSLitTeamMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_SpecColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
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
    unity_DOTS_Sampled_Parallax             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Parallax);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
    unity_DOTS_Sampled_ClearCoatMask        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatMask);
    unity_DOTS_Sampled_ClearCoatSmoothness  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatSmoothness);
    unity_DOTS_Sampled_DetailAlbedoMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailAlbedoMapScale);
    unity_DOTS_Sampled_DetailNormalMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailNormalMapScale);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitTeamMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _SpecColor              unity_DOTS_Sampled_SpecColor
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
#define _Parallax               unity_DOTS_Sampled_Parallax
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength
#define _ClearCoatMask          unity_DOTS_Sampled_ClearCoatMask
#define _ClearCoatSmoothness    unity_DOTS_Sampled_ClearCoatSmoothness
#define _DetailAlbedoMapScale   unity_DOTS_Sampled_DetailAlbedoMapScale
#define _DetailNormalMapScale   unity_DOTS_Sampled_DetailNormalMapScale

#endif

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);
TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_ClearCoatMap);       SAMPLER(sampler_ClearCoatMap);
TEXTURE2D(_TeamMask);           SAMPLER(sampler_TeamMask);
UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_BaseMap);

// URP's Utils headers declare these and build every feature on them.
half4 GetBaseColor()            { return _BaseColor; }
half4 GetSpecColor()            { return _SpecColor; }
half4 GetEmissionColor()        { return _EmissionColor; }
half GetCutoff()                { return _Cutoff; }
half GetSmoothness()            { return _Smoothness; }
half GetMetallic()              { return _Metallic; }
half GetBumpScale()             { return _BumpScale; }
half GetParallax()              { return _Parallax; }
half GetOcclusionStrength()     { return _OcclusionStrength; }
half GetClearCoatMask()         { return _ClearCoatMask; }
half GetClearCoatSmoothness()   { return _ClearCoatSmoothness; }
half GetDetailAlbedoMapScale()  { return _DetailAlbedoMapScale; }
half GetDetailNormalMapScale()  { return _DetailNormalMapScale; }
float4 GetBaseMapST()           { return _BaseMap_ST; }
float4 GetBaseMapTexelSize()    { return _BaseMap_TexelSize; }
float4 GetDetailAlbedoMapST()   { return _DetailAlbedoMap_ST; }

half4 SampleBaseMap(float2 uv)          { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv); }
half4 SampleBumpMap(float2 uv)          { return SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv); }
half4 SampleEmissionMap(float2 uv)      { return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv); }
half4 SampleParallaxMap(float2 uv)      { return SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, uv); }
half4 SampleOcclusionMap(float2 uv)     { return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv); }
half4 SampleDetailMask(float2 uv)       { return SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv); }
half4 SampleDetailAlbedoMap(float2 uv)  { return SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, uv); }
half4 SampleDetailNormalMap(float2 uv)  { return SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uv); }
half4 SampleMetallicGlossMap(float2 uv) { return SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv); }
half4 SampleSpecGlossMap(float2 uv)     { return SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv); }
half4 SampleClearCoatMap(float2 uv)     { return SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, uv); }

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamSurface.hlsl"

#endif
