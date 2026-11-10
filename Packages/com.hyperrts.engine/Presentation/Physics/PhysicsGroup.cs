using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Steps a physics world of its own on presented worlds, for looks only: ragdolls, and the terrain, buildings and
    /// vehicles they land on. Nothing in it is replicated or read by gameplay.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PhysicsGroup : CustomPhysicsSystemGroup
    {
        /// <summary>The <see cref="PhysicsWorldIndex"/> every body in this world carries.</summary>
        public const uint WorldIndex = 1;

        private const uint GroundLayer = 1u << 0;
        private const uint BodyLayer = 1u << 1;

        // The main world's statics belong to the simulation, not to this world.
        public PhysicsGroup() : base(WorldIndex, false)
        {
        }

        /// <summary>What bodies land on, the terrain, buildings and vehicles: it meets bodies only.</summary>
        public static CollisionFilter GroundFilter => new() { BelongsTo = GroundLayer, CollidesWith = BodyLayer };

        /// <summary>
        /// A dynamic body: it meets the ground and other bodies. Bodies joined to each other skip their contacts through
        /// the joint, so only a ragdoll's non-adjacent bones collide.
        /// </summary>
        public static CollisionFilter BodyFilter => new() { BelongsTo = BodyLayer, CollidesWith = GroundLayer | BodyLayer };
    }
}
