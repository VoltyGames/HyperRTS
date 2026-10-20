using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>A road wheel, sprocket or idler that rolls with the ground the unit covers.</summary>
    [Serializable]
    public class WheelSlot : PartSlot
    {
        [Tooltip("Rolling radius in metres; Fill from model measures it from the mesh.")]
        [Min(0.01f)]
        public float radius = 0.4f;

        /// <summary>Half the wheel's height or length in the rig's frame, whichever is larger.</summary>
        public static float MeshRadius(Transform wheel, Transform rig)
        {
            var size = MeshBounds(wheel, rig).size;
            return Mathf.Max(size.y, size.z) * 0.5f;
        }

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = Rest(entity, MotionPartKind.Wheel);
            baked.Axis = part.InverseTransformDirection(rig.right).normalized;
            baked.Size = radius;
            return baked;
        }
    }
}
