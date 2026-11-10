// HyperRTS/LitTeam bone animation. With _BONES on, each vertex is skinned by up to four bones. _BoneTex holds a row per
// frame and three texels per bone, the 3x4 skinning matrix (pose times inverse bind) in object space. Bone 0 is the
// root (the hips). A negative weight marks an upper-body bone, which plays _UpperClip and is stored relative to its
// frame's root, so it rides on the root of _LowerClip, which the rest play. A clip switch blends from the frozen pose
// in _LowerFade or _UpperFade over _BoneFadeTime. With DOTS instancing, _PoseSlot 0 or above draws a live pose (a ragdoll)
// instead: every bone's skinning matrix, absolute in object space, from that slot of the global _PoseSlots buffer.
#ifndef HYPERRTS_LIT_TEAM_BONES_INCLUDED
#define HYPERRTS_LIT_TEAM_BONES_INCLUDED

#ifdef _BONES
#define BONE_INPUT , float4 boneIndex : TEXCOORD6, float4 boneWeight : TEXCOORD7
#else
#define BONE_INPUT
#endif

#ifdef _BONES

TEXTURE2D(_BoneTex);

#ifdef UNITY_DOTS_INSTANCING_ENABLED
// Matches PoseSlots.MostBones: each slot holds this many bones of three rows.
#define POSE_SLOT_BONES 128
StructuredBuffer<float4> _PoseSlots;

float3x4 SlotSkin(uint slot, uint bone)
{
    uint at = (slot * POSE_SLOT_BONES + bone) * 3;
    return float3x4(_PoseSlots[at], _PoseSlots[at + 1], _PoseSlots[at + 2]);
}

void ApplyLivePose(uint slot, float4 boneIndex, float4 boneWeight, inout float3 positionOS, inout float3 normalOS)
{
    float3 position = 0.0;
    float3 normal = 0.0;
    [unroll] for (uint i = 0; i < 4; i++)
    {
        float3x4 pose = SlotSkin(slot, (uint)(boneIndex[i] + 0.5));
        float weight = abs(boneWeight[i]);
        position += weight * mul(pose, float4(positionOS, 1.0));
        normal += weight * mul((float3x3)pose, normalOS);
    }
    positionOS = position;
    normalOS = normalize(normal);
}
#endif

// The frames one body half blends, and how much of its clip shows over the pose it fades out of.
struct BoneSide
{
    float3 frame;
    float3 old;
    float shown;
};

BoneSide BoneSideAt(float4 clip, float4 fade)
{
    BoneSide side;
    side.frame = VATFrameAt(clip, _ClipTime);
    side.old = fade.xyz;
    side.shown = saturate((_ClipTime - fade.w) / max(_BoneFadeTime, 0.0001));
    return side;
}

float3x4 BoneMatrixAt(uint bone, float3 frame)
{
    float3x4 matrices;
    [unroll] for (uint row = 0; row < 3; row++)
    {
        matrices[row] = lerp(LOAD_TEXTURE2D_LOD(_BoneTex, uint2(bone * 3 + row, frame.x), 0),
            LOAD_TEXTURE2D_LOD(_BoneTex, uint2(bone * 3 + row, frame.y), 0), frame.z);
    }
    return matrices;
}

float3x4 BonePose(uint bone, BoneSide side)
{
    float3x4 pose = BoneMatrixAt(bone, side.frame);
    if (side.shown < 1.0)
    {
        pose = lerp(BoneMatrixAt(bone, side.old), pose, side.shown);
    }
    return pose;
}

// outer applied after inner, as 4x4 affine matrices.
float3x4 Compose(float3x4 outer, float3x4 inner)
{
    float3x3 turn = (float3x3)outer;
    float3x3 turned = mul(turn, (float3x3)inner);
    float3 moved = mul(turn, float3(inner._m03, inner._m13, inner._m23)) + float3(outer._m03, outer._m13, outer._m23);
    return float3x4(float4(turned[0], moved.x), float4(turned[1], moved.y), float4(turned[2], moved.z));
}

void ApplyBones(float4 boneIndex, float4 boneWeight, inout float3 positionOS, inout float3 normalOS)
{
#ifdef UNITY_DOTS_INSTANCING_ENABLED
    if (_PoseSlot >= 0.0)
    {
        ApplyLivePose((uint)_PoseSlot, boneIndex, boneWeight, positionOS, normalOS);
        return;
    }
#endif
    BoneSide lower = BoneSideAt(_LowerClip, _LowerFade);
    BoneSide upper = BoneSideAt(_UpperClip, _UpperFade);
    float3x4 hips = BonePose(0, lower);
    float3 position = 0.0;
    float3 normal = 0.0;
    [unroll] for (uint i = 0; i < 4; i++)
    {
        float weight = boneWeight[i];
        if (weight == 0.0)
        {
            continue;
        }
        uint bone = (uint)(boneIndex[i] + 0.5);
        float3x4 pose;
        if (weight < 0.0)
        {
            pose = Compose(hips, BonePose(bone, upper));
        }
        else
        {
            pose = BonePose(bone, lower);
        }
        position += abs(weight) * mul(pose, float4(positionOS, 1.0));
        normal += abs(weight) * mul((float3x3)pose, normalOS);
    }
    positionOS = position;
    normalOS = normalize(normal);
}

#endif

#endif
