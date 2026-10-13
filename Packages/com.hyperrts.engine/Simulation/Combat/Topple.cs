using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Scenery that falls away from its lethal hit, such as a tree.</summary>
    public struct Topple : IComponentData
    {
        /// <summary>Unit trunk direction in the standing model, base to top.</summary>
        public float3 Trunk;

        /// <summary>Heading in radians from +Z toward +X that the crown points in the remains model.</summary>
        public float Lie;
    }
}
