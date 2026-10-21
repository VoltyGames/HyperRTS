using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A barrel or launcher that raises to its weapon's pitch within its limits.</summary>
    [Serializable]
    public class ElevationSlot : WeaponPartSlot
    {
        [Tooltip("Degrees per second it elevates.")]
        [Min(1f)]
        public float elevationSpeed = 25f;

        [Tooltip("Lowest elevation in degrees (negative is depression).")]
        public float minElevation = -10f;

        [Tooltip("Highest elevation in degrees.")]
        public float maxElevation = 20f;

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Elevation);
            baked.Axis = part.InverseTransformDirection(-rig.right).normalized;
            baked.Rate = math.radians(elevationSpeed);
            baked.Min = math.radians(minElevation);
            baked.Max = math.radians(maxElevation);
            return baked;
        }
    }
}
