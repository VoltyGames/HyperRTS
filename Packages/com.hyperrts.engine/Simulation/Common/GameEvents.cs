using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Tags the singleton holding this frame's gameplay events (<see cref="UnitProduced"/>,
    /// <see cref="BuildingCompleted"/>, <see cref="ResearchCompleted"/>, <see cref="OwnerChanged"/>,
    /// <see cref="EntityDamaged"/>). <c>GameEventSystem</c> clears them at the start of
    /// the order phase; sound, stats, cleanup and game systems react to them later in the frame.
    /// </summary>
    public struct GameEvents : IComponentData { }

    /// <summary>A producer finished a unit this frame; the unit itself appears when the frame's commands play back.</summary>
    [InternalBufferCapacity(0)]
    public struct UnitProduced : IBufferElementData
    {
        public Entity Producer;
        public Entity Prefab;
        public byte Faction;
        public float3 Position;
    }

    /// <summary>A building site was finished this frame.</summary>
    [InternalBufferCapacity(0)]
    public struct BuildingCompleted : IBufferElementData
    {
        public Entity Building;
        public byte Faction;
    }

    /// <summary>
    /// An entity changed hands this frame (capture, a game's mind control). Its <see cref="Faction"/> switches to
    /// <see cref="To"/> when the frame's commands play back; write it with <see cref="Ownership.Transfer"/>.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct OwnerChanged : IBufferElementData
    {
        public Entity Entity;
        public byte From;
        public byte To;
    }

    /// <summary>A producer finished a research item this frame.</summary>
    [InternalBufferCapacity(0)]
    public struct ResearchCompleted : IBufferElementData
    {
        public Entity Producer;
        public Entity Prefab;
        public byte Faction;
    }

    /// <summary>An owned entity lost health to a hit this frame; one per hit, so a splash adds one per victim.</summary>
    [InternalBufferCapacity(0)]
    public struct EntityDamaged : IBufferElementData
    {
        public Entity Entity;
        public byte Faction;
    }
}
