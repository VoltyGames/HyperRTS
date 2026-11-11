using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

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

        /// <summary>A collider entity of <paramref name="archetype"/>, which holds <see cref="PhysicsCollider"/>, a transform and <see cref="PhysicsWorldIndex"/>.</summary>
        public static Entity CreateCollider(EntityManager entities, EntityArchetype archetype,
            BlobAssetReference<Collider> shape, in LocalTransform place)
        {
            var entity = entities.CreateEntity(archetype);
            entities.SetComponentData(entity, new PhysicsCollider { Value = shape });
            entities.SetComponentData(entity, place);
            entities.SetSharedComponent(entity, new PhysicsWorldIndex(WorldIndex));
            return entity;
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
