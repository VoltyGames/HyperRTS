using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// The bones, named clips, all-row bounds and ragdoll of one bone animation texture, written by baking a
    /// <c>BoneAnimationSet</c>.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Animation + "Bone Clip Table", fileName = "BoneClipTable")]
    [HelpURL(HyperRTSDocs.Presentation)]
    public class BoneClipTable : ClipTable
    {
        [Tooltip("Bones in texture order: bone i's three texels start at column 3 i.")]
        public List<string> bones = new();

        [Tooltip("Texture rows, one per frame of every clip.")]
        [Min(1)]
        public int frames = 1;

        [Tooltip("Per bone, the index of its parent bone; -1 for none.")]
        public List<int> parents = new();

        [Tooltip("Per bone, its bind pose: from object space into the bone's space. A bone's skin matrix is its pose times this.")]
        public List<Matrix4x4> bindPoses = new();

        [Tooltip("Per bone, whether it belongs to the upper body, whose texture rows are relative to the root bone (bone 0) of the same row.")]
        public List<bool> upperBones = new();

        [Tooltip("Ragdoll bodies, the root body first; empty when the set has no ragdoll.")]
        public List<RagdollBody> ragdoll = new();

        [Tooltip("Per bone, the ragdoll body it rides on rigidly: its own, its nearest ancestor's, else the root body's. Empty without a ragdoll.")]
        public List<int> boneBodies = new();

        public int TextureWidth => bones.Count * 3;
    }
}
