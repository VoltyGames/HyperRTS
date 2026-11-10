// HyperRTS/LitTeam inputs: the material properties and accessors URP's Lit passes read (laid out like URP's
// LitInput.hlsl, minus URP's UV-mapped detail maps, parallax, the specular workflow and transparency) plus the team colour, its mask,
// its height band, the rigid animation clip, the bone animation clips and their fades, the build progress, grime, track lift and the world-scale detail maps. Every LitTeam pass includes this, so the SRP
// Batcher sees one UnityPerMaterial layout.
//   _TeamColor  linear RGB, A how much shows (0 untinted, so neutral needs no special case)
//   _TeamMask   R where team colour goes, on UV0
//   _TeamStrip  x bottom, y top of the band (object-space metres) the mask applies in
//   _BONES      keyword: skinning from _BoneTex, bone indices in UV6 and weights in UV7 (negative for upper-body bones);
//               per instance _LowerClip and _UpperClip, and _LowerFade and _UpperFade, the pose a clip switch fades
//               out of over _BoneFadeTime seconds; _PoseSlot 0 or above draws that live pose slot instead (LitTeamBones.hlsl)
//   _VAT_RIGID  keyword: rigid pieces, each moved by its position in _VATPositions and rotation in _VATRotations at
//               _VATClip, piece column in UV1 and row 0 the rest pose (LitTeamVAT.hlsl)
//   _BUILD      keyword (structures only): pieces appear in their UV3 build order up to _BuildProgress (LitTeamBuild.hlsl)
//   _LightsOn   emission multiplier, 0 dark to 1 lit (structures go dark while offline)
//   _TrackSpan  per instance (track meshes only): front z, back z, axle height, wheel radius in object space; with
//               _TrackLift0 and _TrackLift1, the lift in metres at 8 stations front to back, it bends the lower run (LitTeamTrack.hlsl)
//   _DETAIL     keyword: detail albedo and normal projected in object space (triplanar) at _DetailTiling repeats per metre,
//               faded out between _DetailFadeStart and _DetailFadeEnd metres from the camera (LitTeamDetail.hlsl)
//   _Grime      dust, mud, wet, soot 0 to 1 and _GrimeBand ground height, mud top, dust top (object space); LitTeamGrime.hlsl,
//               colours from the _HyperRTSDustColor and _HyperRTSMudColor globals (ground vehicles only)
// TeamColorSystem writes _TeamColor and _TeamStrip per entity (DOTS instancing), BuildRevealSystem writes _BuildProgress,
// BuildingLightsSystem writes _LightsOn, MotionRigSystem writes _TrackLift0 and _TrackLift1, GrimeSystem writes _Grime and _GrimeBand, game code writes VATClip, the bone clips and _PoseSlot.
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
float4 _LowerClip;
float4 _UpperClip;
float4 _LowerFade;
float4 _UpperFade;
float4 _VATPositions_TexelSize;
float _BoneFadeTime;
float _PoseSlot;
float4 _Grime;
float4 _GrimeBand;
float4 _TrackLift0;
float4 _TrackLift1;
float4 _TrackSpan;
float _CraterSink;
float _BuildProgress;
float _Reveal;
float _BuildDropHeight;
float _BuildDropSlice;
float _BuildDropFrom;
float _DetailTiling;
float _DetailAlbedoStrength;
float _DetailNormalStrength;
float _DetailFadeStart;
float _DetailFadeEnd;
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
    UNITY_DOTS_INSTANCED_PROP(float4, _LowerClip)
    UNITY_DOTS_INSTANCED_PROP(float4, _UpperClip)
    UNITY_DOTS_INSTANCED_PROP(float4, _LowerFade)
    UNITY_DOTS_INSTANCED_PROP(float4, _UpperFade)
    UNITY_DOTS_INSTANCED_PROP(float4, _Grime)
    UNITY_DOTS_INSTANCED_PROP(float4, _GrimeBand)
    UNITY_DOTS_INSTANCED_PROP(float4, _TrackLift0)
    UNITY_DOTS_INSTANCED_PROP(float4, _TrackLift1)
    UNITY_DOTS_INSTANCED_PROP(float4, _TrackSpan)
    UNITY_DOTS_INSTANCED_PROP(float , _PoseSlot)
    UNITY_DOTS_INSTANCED_PROP(float , _BuildProgress)
    UNITY_DOTS_INSTANCED_PROP(float , _Reveal)
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
static float4 unity_DOTS_Sampled_LowerClip;
static float4 unity_DOTS_Sampled_UpperClip;
static float4 unity_DOTS_Sampled_LowerFade;
static float4 unity_DOTS_Sampled_UpperFade;
static float4 unity_DOTS_Sampled_Grime;
static float4 unity_DOTS_Sampled_GrimeBand;
static float4 unity_DOTS_Sampled_TrackLift0;
static float4 unity_DOTS_Sampled_TrackLift1;
static float4 unity_DOTS_Sampled_TrackSpan;
static float  unity_DOTS_Sampled_PoseSlot;
static float  unity_DOTS_Sampled_BuildProgress;
static float  unity_DOTS_Sampled_Reveal;
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
    unity_DOTS_Sampled_LowerClip             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _LowerClip);
    unity_DOTS_Sampled_UpperClip             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _UpperClip);
    unity_DOTS_Sampled_LowerFade             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _LowerFade);
    unity_DOTS_Sampled_UpperFade             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _UpperFade);
    unity_DOTS_Sampled_Grime                = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Grime);
    unity_DOTS_Sampled_GrimeBand            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _GrimeBand);
    unity_DOTS_Sampled_TrackLift0           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TrackLift0);
    unity_DOTS_Sampled_TrackLift1           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TrackLift1);
    unity_DOTS_Sampled_TrackSpan            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TrackSpan);
    unity_DOTS_Sampled_PoseSlot             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _PoseSlot);
    unity_DOTS_Sampled_BuildProgress        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BuildProgress);
    unity_DOTS_Sampled_Reveal               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Reveal);
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
#define _LowerClip               unity_DOTS_Sampled_LowerClip
#define _UpperClip               unity_DOTS_Sampled_UpperClip
#define _LowerFade               unity_DOTS_Sampled_LowerFade
#define _UpperFade               unity_DOTS_Sampled_UpperFade
#define _Grime                  unity_DOTS_Sampled_Grime
#define _GrimeBand              unity_DOTS_Sampled_GrimeBand
#define _TrackLift0             unity_DOTS_Sampled_TrackLift0
#define _TrackLift1             unity_DOTS_Sampled_TrackLift1
#define _TrackSpan              unity_DOTS_Sampled_TrackSpan
#define _PoseSlot               unity_DOTS_Sampled_PoseSlot
#define _BuildProgress          unity_DOTS_Sampled_BuildProgress
#define _Reveal                 unity_DOTS_Sampled_Reveal
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

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamSurface.hlsl"

// Drops a growing share of a mesh's pixels in a 4 by 4 ordered pattern as _Reveal falls, so a unit fading into the
// fog thins out instead of popping. 1 keeps every pixel.
void ClipReveal(float4 positionCS)
{
    static const float pattern[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 pixel = uint2(positionCS.xy) & 3;
    clip(_Reveal - (pattern[pixel.y * 4 + pixel.x] + 0.5) / 16.0);
}

#endif
