// HyperRTS/LitTeam bone animation. With _BONES on, each vertex is skinned by up to four bones. _BoneTex holds a row per
// frame and three texels per bone, the 3x4 skinning matrix (pose times inverse bind) in object space. A negative
// weight marks an upper-body bone, which plays _UpperClip; the rest play _LowerClip. A clip switch blends from the
// frozen pose in _LowerFade or _UpperFade over _BoneFadeTime.
#ifndef HYPERRTS_LIT_TEAM_BONES_INCLUDED
#define HYPERRTS_LIT_TEAM_BONES_INCLUDED

#ifdef _BONES
#define BONE_INPUT , float4 boneIndex : TEXCOORD6, float4 boneWeight : TEXCOORD7
#else
#define BONE_INPUT
#endif

#ifdef _BONES

TEXTURE2D(_BoneTex);

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

void ApplyBones(float4 boneIndex, float4 boneWeight, inout float3 positionOS, inout float3 normalOS)
{
    BoneSide lower = BoneSideAt(_LowerClip, _LowerFade);
    BoneSide upper = BoneSideAt(_UpperClip, _UpperFade);
    float3 position = 0.0;
    float3 normal = 0.0;
    [unroll] for (uint i = 0; i < 4; i++)
    {
        float weight = boneWeight[i];
        if (weight == 0.0)
        {
            continue;
        }
        BoneSide side = lower;
        if (weight < 0.0)
        {
            side = upper;
        }
        float3x4 pose = BonePose((uint)(boneIndex[i] + 0.5), side);
        position += abs(weight) * mul(pose, float4(positionOS, 1.0));
        normal += abs(weight) * mul((float3x3)pose, normalOS);
    }
    positionOS = position;
    normalOS = normalize(normal);
}

#endif

#endif
