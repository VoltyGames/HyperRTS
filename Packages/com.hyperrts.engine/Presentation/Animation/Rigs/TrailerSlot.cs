using System;
using System.Linq;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>
    /// A trailer pivoting on its hitch that swings after the hull as a towed trailer does. Looks only: the unit still
    /// moves and collides as one rigid hull.
    /// </summary>
    [Serializable]
    public class TrailerSlot : PartSlot
    {
        [Tooltip("Metres from the hitch (the part's pivot) back to the middle of the trailer's axles; Fill from model measures it from its wheels.")]
        [Unit("m")]
        [Min(0.5f)]
        public float axleDistance = 7f;

        [Tooltip("Largest angle between trailer and hull, in degrees, short of a jackknife.")]
        [Range(5f, 90f)]
        public float maxAngle = 60f;

        /// <summary>Metres from the trailer's pivot back to the middle of the wheels below it, in the rig's frame.</summary>
        public static float MeasureAxleDistance(Transform trailer, Transform rig)
        {
            var wheels = trailer.GetComponentsInChildren<Transform>(true).Where(child => child.name.StartsWith("Wheel_")).ToList();
            if (wheels.Count == 0)
            {
                throw new InvalidOperationException($"'{trailer.name}' has no Wheel_ parts to measure its axles from.");
            }

            var hitch = rig.InverseTransformPoint(trailer.position).z;
            return hitch - wheels.Average(wheel => rig.InverseTransformPoint(wheel.position).z);
        }

        public override MotionRigPart Bake(Transform rig, Entity entity)
        {
            var baked = AtRest(entity, MotionPartKind.Trailer);
            // Down, so a positive angle swings the trailer to the left, as a positive curvature turns.
            baked.Axis = part.InverseTransformDirection(-rig.up).normalized;
            baked.Size = axleDistance;
            baked.Rate = maxAngle * Mathf.Deg2Rad;
            return baked;
        }
    }
}
