using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A rotor that spins up while the unit flies and down when it docks.</summary>
    [Serializable]
    public class RotorSlot : PartSlot
    {
        [Tooltip("Local axis it spins about.")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Revolutions per minute at full speed.")]
        [Min(1f)]
        public float maxRPM = 300f;

        [Tooltip("Seconds from standing to full speed.")]
        [Min(0.01f)]
        [FormerlySerializedAs("spoolTime")]
        public float spinUpTime = 4f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Rotor);
            baked.Axis = axis.normalized;
            baked.Rate = maxRPM * 2f * math.PI / 60f;
            baked.Time = spinUpTime;
            return baked;
        }
    }
}
