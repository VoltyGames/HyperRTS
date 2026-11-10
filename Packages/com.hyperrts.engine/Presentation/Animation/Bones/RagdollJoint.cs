using System;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// How a ragdoll body swings and twists on its parent, as <c>PhysicsJoint.CreateRagdoll</c> takes it: the twist
    /// axis is the body's capsule, the angles are in degrees.
    /// </summary>
    [Serializable]
    public struct RagdollJoint
    {
        [Tooltip("Axis across the capsule, in the bone's axes, that the perpendicular range swings about: the bend axis of a knee or elbow.")]
        public Vector3 perpendicularAxis;

        [Tooltip("Half angle of the cone the capsule may swing in from its rest direction, in degrees.")]
        [Range(0f, 180f)]
        public float coneAngle;

        [Tooltip("Lowest swing about the perpendicular axis, in degrees; with Perpendicular Max it trims the cone.")]
        [Range(-90f, 90f)]
        public float perpendicularMin;

        [Tooltip("Highest swing about the perpendicular axis, in degrees.")]
        [Range(-90f, 90f)]
        public float perpendicularMax;

        [Tooltip("Lowest twist about the capsule, in degrees.")]
        [Range(-180f, 180f)]
        public float twistMin;

        [Tooltip("Highest twist about the capsule, in degrees.")]
        [Range(-180f, 180f)]
        public float twistMax;
    }
}
