using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A turret that follows its weapon's simulated yaw; the weapon sets the traverse rate.</summary>
    [Serializable]
    public class TurretSlot : WeaponPartSlot
    {
        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Turret);
            baked.Axis = part.InverseTransformDirection(rig.up).normalized;
            return baked;
        }
    }
}
