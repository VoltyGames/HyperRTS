// HyperRTS/LitTeam vertex animation textures. With _VAT on, each pass's vertex function takes its pose from
// _VATPositions and _VATNormals at the instance's _VATClip and the global _VATTime. UV1 holds each vertex's texel:
// u = (column + 0.5) / width, v = row within the frame, so it survives Unity splitting vertices on import.
// Include it after the pass file: it wraps whichever pass vertex function that file defined.
#ifndef HYPERRTS_LIT_TEAM_VAT_INCLUDED
#define HYPERRTS_LIT_TEAM_VAT_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"

// Passes whose Attributes lack TEXCOORD1 take it as a separate input.
#ifdef _VAT
#define VAT_INDEX_INPUT , float2 vatIndex : TEXCOORD1
#else
#define VAT_INDEX_INPUT
#endif

// Mirrors VATClip.FrameAt on the C# side: x frame, y next frame, z blend.
float3 VATFrameAt(float4 clip, float time)
{
    float frames = max(clip.y, 1.0);
    float position = max(time - clip.w, 0.0) * abs(clip.z);
    bool loops = clip.z > 0.0;
    float frame = loops ? fmod(position, frames) : min(position, frames - 1.0);
    float first = floor(frame);
    float next = loops ? fmod(first + 1.0, frames) : min(first + 1.0, frames - 1.0);
    return float3(clip.x + first, clip.x + next, frame - first);
}

TEXTURE2D(_VATPositions);
TEXTURE2D(_VATNormals);
float _VATTime;

uint2 VATTexel(float2 vatIndex, float frame)
{
    return uint2(floor(vatIndex.x * _VATPositions_TexelSize.z), frame * _VATRowsPerFrame + floor(vatIndex.y + 0.5));
}

#define VAT_LOAD(texture, vatIndex, frame)     lerp(LOAD_TEXTURE2D_LOD(texture, VATTexel(vatIndex, frame.x), 0).xyz,          LOAD_TEXTURE2D_LOD(texture, VATTexel(vatIndex, frame.y), 0).xyz, frame.z)

float3 VATPosition(float2 vatIndex)
{
    float3 frame = VATFrameAt(_VATClip, _VATTime);
    return VAT_LOAD(_VATPositions, vatIndex, frame);
}

void ApplyVAT(float2 vatIndex, inout float3 positionOS, inout float3 normalOS)
{
    float3 frame = VATFrameAt(_VATClip, _VATTime);
    positionOS = VAT_LOAD(_VATPositions, vatIndex, frame);
    normalOS = normalize(VAT_LOAD(_VATNormals, vatIndex, frame));
}

// The instance's _VATClip is only readable once its DOTS property cache is set up, so each wrapper does that first.
// The forward pass reads UV1 as URP's static lightmap UV, which VAT renderers never use.
#if defined(UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
Varyings LitTeamForwardVertex(Attributes input)
{
#ifdef _VAT
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyVAT(input.staticLightmapUV, input.positionOS.xyz, input.normalOS);
#endif
    return LitPassVertex(input);
}
#elif defined(UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED)
Varyings LitTeamShadowVertex(Attributes input VAT_INDEX_INPUT)
{
#ifdef _VAT
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyVAT(vatIndex, input.positionOS.xyz, input.normalOS);
#endif
    return ShadowPassVertex(input);
}
#elif defined(UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED)
Varyings LitTeamDepthOnlyVertex(Attributes input VAT_INDEX_INPUT)
{
#ifdef _VAT
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    ApplyVAT(vatIndex, input.position.xyz, input.normalOS);
#else
    input.position.xyz = VATPosition(vatIndex);
#endif
#endif
    return DepthOnlyVertex(input);
}
#elif defined(UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED)
Varyings LitTeamDepthNormalsVertex(Attributes input VAT_INDEX_INPUT)
{
#ifdef _VAT
    UNITY_SETUP_INSTANCE_ID(input);
    ApplyVAT(vatIndex, input.positionOS.xyz, input.normal);
#endif
    return DepthNormalsVertex(input);
}
#elif defined(UNIVERSAL_OBJECT_MOTION_VECTORS_INCLUDED)
// Previous and current pose read the same frame, so only the transform's motion shows.
Varyings LitTeamMotionVectorsVertex(Attributes input VAT_INDEX_INPUT)
{
#ifdef _VAT
    UNITY_SETUP_INSTANCE_ID(input);
    input.position.xyz = VATPosition(vatIndex);
#endif
    return vert(input);
}
#endif

#endif
