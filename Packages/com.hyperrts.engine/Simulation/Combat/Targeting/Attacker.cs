using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;

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
    }
}
