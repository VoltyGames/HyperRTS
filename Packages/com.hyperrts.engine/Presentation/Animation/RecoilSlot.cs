using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A barrel that kicks back along itself on each shot of its weapon.</summary>
    [Serializable]
    public class RecoilSlot : WeaponPartSlot
    {
        [Tooltip("Metres it kicks back.")]
        [Min(0f)]
        public float distance = 0.3f;

        [Tooltip("Seconds it takes to run back out.")]
        [Min(0.01f)]
        public float returnTime = 0.5f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Recoil);
            baked.Axis = part.InverseTransformDirection(-rig.forward).normalized;
            // Local positions are in the parent's units.
            baked.Size = distance / part.parent.lossyScale.x;
            baked.Time = returnTime;
            return baked;
        }
    }
}
