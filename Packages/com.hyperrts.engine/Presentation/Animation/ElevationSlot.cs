using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A barrel or launcher that follows its weapon's simulated pitch; the weapon sets the rate and limits.</summary>
    [Serializable]
    public class ElevationSlot : WeaponPartSlot
    {
        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Elevation);
            baked.Axis = part.InverseTransformDirection(-rig.right).normalized;
            return baked;
        }
    }
}
