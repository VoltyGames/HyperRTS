// Vertex animation for units: rigid pieces moved by a baked clip texture (VAT), or bone skinning, plus cloth waving,
// build reveal, track bend and crater sink.
#ifndef HYPERRTS_LIT_TEAM_DEFORM_INCLUDED
#define HYPERRTS_LIT_TEAM_DEFORM_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamBuild.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamCloth.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamTrack.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Craters/CraterSink.hlsl"

// Passes whose Attributes lack TEXCOORD1 take it as a separate input.
#ifdef _VAT_RIGID
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
TEXTURE2D(_VATRotations);
float _ClipTime;
// ClipClockSystem's time a frame ago, for the motion vector pass.
float _LastClipTime;

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamBones.hlsl"

float3 VATRotate(float4 q, float3 v)
{
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

// Moves a rigid piece's vertex from where row 0 rests it to where the clip's frame has it; normals only turn.
void ApplyRigidVAT(float2 vatIndex, float time, inout float3 positionOS, inout float3 normalOS)
{
    float3 frame = VATFrameAt(_VATClip, time);
    uint column = floor(vatIndex.x * _VATPositions_TexelSize.z);
    float3 rest = LOAD_TEXTURE2D_LOD(_VATPositions, uint2(column, 0), 0).xyz;
    float3 position = lerp(LOAD_TEXTURE2D_LOD(_VATPositions, uint2(column, frame.x), 0).xyz,
        LOAD_TEXTURE2D_LOD(_VATPositions, uint2(column, frame.y), 0).xyz, frame.z);
    float4 rotation = normalize(lerp(LOAD_TEXTURE2D_LOD(_VATRotations, uint2(column, frame.x), 0),
        LOAD_TEXTURE2D_LOD(_VATRotations, uint2(column, frame.y), 0), frame.z));
    positionOS = position + VATRotate(rotation, positionOS - rest);
    normalOS = VATRotate(rotation, normalOS);
}

// Bends a track mesh over its wheels, then sinks the object into any crater under it, as far as _CraterSink lets it.
float3 DeformObject(float3 positionOS)
{
    float3 bent = BendTrack(positionOS);
    return lerp(bent, SinkObjectIntoCrater(bent), _CraterSink);
}

#if defined(_VAT_RIGID)
    #define LIT_TEAM_POSE(vatIndex, positionOS, normalOS, time) ApplyRigidVAT(vatIndex, time, positionOS, normalOS);
#elif defined(_BONES)
    #define LIT_TEAM_POSE(vatIndex, positionOS, normalOS, time) ApplyBones(boneIndex, boneWeight, time, positionOS, normalOS);
#else
    #define LIT_TEAM_POSE(vatIndex, positionOS, normalOS, time)
#endif

#ifdef _CLOTH
    #define LIT_TEAM_CLOTH(positionOS, normalOS, time) ApplyCloth(time, positionOS, normalOS);
#else
    #define LIT_TEAM_CLOTH(positionOS, normalOS, time)
#endif

#ifdef _BUILD
    #define LIT_TEAM_BUILD(positionOS, normalOS) ApplyBuild(buildOrder, positionOS, normalOS);
#else
    #define LIT_TEAM_BUILD(positionOS, normalOS)
#endif

// Every LitTeam pass moves its vertices through this, so all of them agree on depth. Reads the buildOrder, boneIndex
// and boneWeight inputs. The _AT form poses the clips at another time, as the motion vector pass does for last frame.
// Cloth waves before the build reveal, so a piece not yet built still collapses to the origin.
#define LIT_TEAM_DEFORM_AT(vatIndex, positionOS, normalOS, time) \
    LIT_TEAM_POSE(vatIndex, positionOS, normalOS, time) \
    LIT_TEAM_CLOTH(positionOS, normalOS, time) \
    LIT_TEAM_BUILD(positionOS, normalOS) \
    positionOS = DeformObject(positionOS);
#define LIT_TEAM_DEFORM(vatIndex, positionOS, normalOS) LIT_TEAM_DEFORM_AT(vatIndex, positionOS, normalOS, _ClipTime)

#endif
