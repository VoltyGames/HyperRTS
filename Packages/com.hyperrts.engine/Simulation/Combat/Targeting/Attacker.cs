using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>The parts of one engaging unit the <see cref="Engager"/> reads, which its jobs gather per entity.</summary>
    internal struct Attacker
    {
        public Entity Entity;
        public float3 Position;
        public DynamicBuffer<WeaponMount> Mounts;
        public VisionRange Vision;
        public Owner Owner;
        public UnitMove Moves;

        public Attacker(Entity entity, in LocalTransform transform, DynamicBuffer<WeaponMount> mounts, VisionRange vision,
            Owner owner)
        {
            Entity = entity;
            Position = transform.Position;
            Mounts = mounts;
            Vision = vision;
            Owner = owner;
            Moves = default;
        }
    }
}
