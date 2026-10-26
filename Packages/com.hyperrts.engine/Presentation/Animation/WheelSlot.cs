using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A road wheel, sprocket or idler that rolls with the ground the unit covers; steered wheels also turn.</summary>
    [Serializable]
    public class WheelSlot : PartSlot
    {
        [Tooltip("Rolling radius in metres; Fill from model measures it from the mesh.")]
        [Min(0.01f)]
        public float radius = 0.4f;

        [Tooltip("Turns with the steering; Fill from model sets it on the Locomotion's steering axles.")]
        public bool steers;

        /// <summary>Half the wheel's height or length in the rig's frame, whichever is larger.</summary>
        public static float MeshRadius(Transform wheel, Transform rig)
        {
            var size = MeshBounds(wheel, rig).size;
            return Mathf.Max(size.y, size.z) * 0.5f;
        }

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Wheel);
            baked.Axis = part.InverseTransformDirection(rig.right).normalized;
            baked.Size = radius;
            return baked;
        }

        /// <summary>The baked wheel made a steering one, <paramref name="rearAxle"/> being the Locomotion's.</summary>
        public MotionRigPart Steering(MotionRigPart baked, Transform rig, float rearAxle)
        {
            baked.Kind = MotionPartKind.SteeringWheel;
            // Down, so a positive angle steers left as a positive curvature turns.
            baked.SteerAxis = part.InverseTransformDirection(-rig.up).normalized;
            baked.RearAxleDistance = rig.InverseTransformPoint(part.position).z + rearAxle;
            return baked;
        }
    }
}
