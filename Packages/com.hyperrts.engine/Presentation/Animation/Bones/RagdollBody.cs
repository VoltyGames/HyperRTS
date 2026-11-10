using System;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// One baked ragdoll body: a capsule on a bone of the texture, and its joint to its parent body. A body's frame is
    /// its bone's rest position and rotation in object space, without scale.
    /// </summary>
    [Serializable]
    public struct RagdollBody
    {
        [Tooltip("Index of the bone, in texture order, the body moves.")]
        public int bone;

        [Tooltip("Index of the parent body in the table's ragdoll; -1 for the root body, which has no joint.")]
        public int parent;

        [Tooltip("One end of the capsule's core, in metres in the bone's axes.")]
        public Vector3 capsuleStart;

        [Tooltip("The other end, away from the joint; the capsule runs from Start to End.")]
        public Vector3 capsuleEnd;

        [Tooltip("Capsule radius in metres.")]
        public float radius;

        [Tooltip("This body's share of the whole ragdoll's mass.")]
        public float massShare;

        public RagdollJoint joint;
    }
}
