using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Scenery that falls over away from its lethal hit (a tree): its remains lie toward the fall.</summary>
    public struct Topple : IComponentData
    {
        /// <summary>Unit trunk direction in the standing model, base to top.</summary>
        public float3 Trunk;

        /// <summary>Heading (radians from +Z toward +X) the crown points in the remains model.</summary>
        public float Lie;
    }
}
