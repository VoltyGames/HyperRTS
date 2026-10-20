using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Something units can't drive through, such as a wreck: a capsule that pushes them clear without closing
    /// <see cref="NavGrid"/> cells. Disabled, it stops blocking.
    /// </summary>
    public struct Blocker : IComponentData, IEnableableComponent
    {
        public float Radius;

        /// <summary>Full length along the forward; up to the diameter keeps it round.</summary>
        public float HullLength;

        /// <summary>Half the capsule's core segment on XZ, along the entity's facing.</summary>
        public readonly float2 SegmentAxis(quaternion rotation) =>
            math.normalizesafe(math.mul(rotation, math.forward()).xz) * math.max(0f, HullLength * 0.5f - Radius);
    }
}
