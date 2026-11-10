using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// A bone texture read on the CPU the way LitTeamBones.hlsl reads it on the GPU, so a pose taken from it matches the
    /// one drawn. The texture must be readable.
    /// </summary>
    public readonly struct BoneTexture
    {
        private readonly NativeArray<half4> _texels;
        private readonly int _width;

        public BoneTexture(Texture2D texture)
        {
            if (!texture.isReadable || texture.format != TextureFormat.RGBAHalf)
            {
                throw new InvalidOperationException($"Bone texture '{texture.name}' must be readable RGBAHalf for ragdolls to read poses from it.");
            }

            _texels = texture.GetPixelData<half4>(0);
            _width = texture.width;
        }

        /// <summary>The skinning matrix of <paramref name="bone"/> at <paramref name="side"/>'s rows and fade.</summary>
        public float4x4 Pose(int bone, in BoneSide side)
        {
            var pose = Skin(bone, side.Frame);
            return side.Shown < 1f ? Lerp(Skin(bone, side.Old), pose, side.Shown) : pose;
        }

        private float4x4 Skin(int bone, float3 frame) =>
            Lerp(Skin(bone, (int)frame.x), Skin(bone, (int)frame.y), frame.z);

        private float4x4 Skin(int bone, int row)
        {
            var at = row * _width + bone * 3;
            var rows = new float4x4((float4)_texels[at], (float4)_texels[at + 1], (float4)_texels[at + 2], new float4(0f, 0f, 0f, 1f));
            return math.transpose(rows);
        }

        private static float4x4 Lerp(in float4x4 from, in float4x4 to, float t) => new(
            math.lerp(from.c0, to.c0, t), math.lerp(from.c1, to.c1, t), math.lerp(from.c2, to.c2, t), math.lerp(from.c3, to.c3, t));
    }
}
