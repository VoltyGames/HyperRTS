using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A turret that traverses to its weapon's yaw.</summary>
    [Serializable]
    public class TurretSlot : PosedSlot
    {
        [Tooltip("Degrees per second it traverses.")]
        [Min(1f)]
        public float traverseSpeed = 40f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = Rest(entity, MotionPartKind.Turret);
            baked.Pose = (byte)pose;
            baked.Axis = part.InverseTransformDirection(rig.up).normalized;
            baked.Rate = math.radians(traverseSpeed);
            return baked;
        }
    }
}
