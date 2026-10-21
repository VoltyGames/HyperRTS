using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A turret that traverses to its weapon's yaw.</summary>
    [Serializable]
    public class TurretSlot : WeaponPartSlot
    {
        [Tooltip("Degrees per second it traverses.")]
        [Min(1f)]
        public float traverseSpeed = 40f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Turret);
            baked.Axis = part.InverseTransformDirection(rig.up).normalized;
            baked.Rate = math.radians(traverseSpeed);
            return baked;
        }
    }
}
