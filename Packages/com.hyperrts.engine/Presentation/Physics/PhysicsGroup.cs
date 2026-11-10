using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace HyperRTS.Presentation.Physics
{
    /// <summary>
    /// Steps a physics world of its own on presented worlds, for looks only: ragdolls and the terrain they land on.
    /// Nothing in it is replicated or read by gameplay.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PhysicsGroup : CustomPhysicsSystemGroup
    {
        /// <summary>The <see cref="PhysicsWorldIndex"/> every body in this world carries.</summary>
        public const uint WorldIndex = 1;

        private const uint TerrainLayer = 1u << 0;
        private const uint BodyLayer = 1u << 1;

        // The main world's statics belong to the simulation, not to this world.
        public PhysicsGroup() : base(WorldIndex, false)
        {
        }

        /// <summary>The ground: it meets bodies only.</summary>
        public static CollisionFilter TerrainFilter => new() { BelongsTo = TerrainLayer, CollidesWith = BodyLayer };

        /// <summary>
        /// A dynamic body: it meets the ground and other bodies. Bodies joined to each other skip their contacts through
        /// the joint, so only a ragdoll's non-adjacent bones collide.
        /// </summary>
        public static CollisionFilter BodyFilter => new() { BelongsTo = BodyLayer, CollidesWith = TerrainLayer | BodyLayer };
    }
}
