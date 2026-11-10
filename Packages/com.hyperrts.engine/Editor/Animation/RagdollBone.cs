using System;
using HyperRTS.Presentation.Animation.Bones;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>One ragdoll body of a bone set as edited: a capsule on a rig bone and its joint to the parent body's bone.</summary>
    [Serializable]
    public class RagdollBone
    {
        [Tooltip("The rig bone the body moves; the bones below it with no body of their own ride on it.")]
        public string bone;

        [Tooltip("Bone of the parent body, an ancestor of Bone; empty for the root body, which comes first.")]
        public string parentBone;

        [Tooltip("One end of the capsule's core, in metres in the bone's axes from its origin.")]
        public Vector3 capsuleStart;

        [Tooltip("The other end, away from the joint.")]
        public Vector3 capsuleEnd;

        [Tooltip("Capsule radius in metres.")]
        [Min(0.01f)]
        public float radius = 0.05f;

        [Tooltip("This body's share of the whole ragdoll's mass.")]
        [Min(0.001f)]
        public float massShare = 0.1f;

        [Tooltip("The joint to the parent body; the root body has none.")]
        public RagdollJoint joint;

        public RagdollBody Bake(int boneIndex, int parentIndex) => new()
        {
            bone = boneIndex,
            parent = parentIndex,
            capsuleStart = capsuleStart,
            capsuleEnd = capsuleEnd,
            radius = radius,
            massShare = massShare,
            joint = joint,
        };
    }
}
