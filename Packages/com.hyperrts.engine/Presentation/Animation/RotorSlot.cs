using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A rotor that spools up while the unit flies and down when it docks.</summary>
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
        public float spoolTime = 4f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = Rest(entity, MotionPartKind.Rotor);
            baked.Axis = axis.normalized;
            baked.Rate = maxRPM * 2f * math.PI / 60f;
            baked.Time = spoolTime;
            return baked;
        }
    }
}
