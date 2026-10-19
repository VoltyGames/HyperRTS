using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>A side the match has content for, on the Match entity. With none, players keep their baked build orders.</summary>
    [InternalBufferCapacity(0)]
    public struct MatchSide : IBufferElementData
    {
        public byte Value;
    }

    /// <summary>One start kit piece of a side, on the Match entity.</summary>
    [InternalBufferCapacity(0)]
    public struct SideStartItem : IBufferElementData
    {
        public byte Side;
        public Entity Prefab;

        /// <summary>Metres right (x) and forward (y) of the spawn.</summary>
        public float2 Offset;

        /// <summary>Radians, added to the spawn's yaw.</summary>
        public float Yaw;
    }

    /// <summary>One AI build order step of a side, on the Match entity.</summary>
    [InternalBufferCapacity(0)]
    public struct SideBuildStep : IBufferElementData
    {
        public byte Side;
        public Entity Prefab;
        public int Count;
    }

    /// <summary>Where a player's start kit goes; yaw in radians.</summary>
    public struct PlayerSpawn : IComponentData
    {
        public float3 Position;
        public float Yaw;
    }

    /// <summary>Marks the Match once every player has its side's build order and start kit.</summary>
    public struct SidesApplied : IComponentData
    {
    }
}
