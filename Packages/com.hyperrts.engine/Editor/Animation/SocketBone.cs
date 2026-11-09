using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>A socket baked as a bone: its node, the skeleton bone carrying it and, for gear held in both hands, where the other hand holds it.</summary>
    internal readonly struct SocketBone
    {
        public readonly Transform Node;
        public readonly int Carrier;
        public readonly Transform Support;
        public readonly Transform Hand;
        public readonly Matrix4x4 Bind;

        public SocketBone(Transform node, int carrier, Transform support, Transform hand, Matrix4x4 bind)
        {
            (Node, Carrier, Support, Hand, Bind) = (node, carrier, support, hand, bind);
        }

        /// <summary>The node's world matrix, swung about its origin so the support points at the other hand when both hands hold the gear.</summary>
        public Matrix4x4 Placed(bool bothHands)
        {
            var placed = Node.localToWorldMatrix;
            if (!bothHands || Support == null)
            {
                return placed;
            }

            var grip = Node.position;
            var swing = Quaternion.FromToRotation(Support.position - grip, Hand.position - grip);
            return Matrix4x4.Translate(grip) * Matrix4x4.Rotate(swing) * Matrix4x4.Translate(-grip) * placed;
        }
    }
}
