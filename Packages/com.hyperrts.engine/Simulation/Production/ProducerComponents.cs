using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Trains the units in its <see cref="ProductionQueueItem"/> queue, one at a time.</summary>
    public struct Producer : IComponentData
    {
        public float3 SpawnOffset;
        public int QueueLimit;

        /// <summary>Production speed multiplier (1 = build times as listed) from stats; low power slows it further.</summary>
        public float Speed;

        /// <summary>Seconds of work done on the head of the queue.</summary>
        [GhostField] public float Elapsed;
    }

    /// <summary>A unit prefab a producer can train.</summary>
    [InternalBufferCapacity(6)]
    public struct ProductionOption : IBufferElementData, IPrefabOption
    {
        public Entity Prefab;

        Entity IPrefabOption.Prefab => Prefab;
    }

    /// <summary>Where new units walk after spawning; disabled means stay at the spawn point.</summary>
    public struct RallyPoint : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }

    /// <summary>A new unit's move to the rally point, issued by <see cref="RallyOrderSystem"/>.</summary>
    public struct RallyOrder : IComponentData
    {
        public float3 Position;
    }
}
