using System;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// When a dying figure's ragdoll takes over from its fall clip, how a blast throws it and when it comes to rest; set
    /// on the bone set beside its ragdoll bodies.
    /// </summary>
    [Serializable]
    public struct RagdollTuning
    {
        [Tooltip("Seconds of its fall clip a dying figure plays before it becomes a ragdoll.")]
        [Unit("s")]
        [Min(0f)]
        public float handoverTime;

        [Tooltip("Distance from the camera within which a dying figure becomes a ragdoll; farther ones keep their fall clip.")]
        [Unit("m")]
        [Min(0f)]
        public float cameraDistance;

        [Tooltip("Mass of the whole body, shared among the bodies by their mass shares.")]
        [Unit("kg")]
        [Min(1f)]
        public float mass;

        [Tooltip("Speed a blast throws the body away from it at.")]
        [Unit("m/s")]
        [Min(0f)]
        public float blastSpeed;

        [Tooltip("Upward speed a blast adds.")]
        [Unit("m/s")]
        [Min(0f)]
        public float blastLift;

        [Tooltip("How fast the bodies lose their spin, per second.")]
        [Min(0f)]
        public float angularDamping;

        [Tooltip("Speed, in metres or radians per second, under which a body counts as at rest.")]
        [Min(0f)]
        public float restSpeed;

        [Tooltip("Seconds every body must stay at rest before the ragdoll freezes in its pose.")]
        [Unit("s")]
        [Min(0f)]
        public float restTime;

        [Tooltip("Seconds after the handover at which the ragdoll freezes at the latest.")]
        [Unit("s")]
        [Min(0f)]
        public float settleTime;

        /// <summary>World velocity <paramref name="death"/> throws the body at: away from a blast <paramref name="towardBlast"/> and up, else none.</summary>
        public readonly float3 Throw(DeathCause death, float2 towardBlast)
        {
            if (death != DeathCause.Blast)
            {
                return float3.zero;
            }

            return new float3(-towardBlast.x * blastSpeed, blastLift, -towardBlast.y * blastSpeed);
        }

        public static RagdollTuning Default => new()
        {
            handoverTime = 0.2f,
            cameraDistance = 80f,
            mass = 80f,
            blastSpeed = 4f,
            blastLift = 3f,
            angularDamping = 0.5f,
            restSpeed = 0.15f,
            restTime = 0.3f,
            settleTime = 2.5f,
        };
    }
}
