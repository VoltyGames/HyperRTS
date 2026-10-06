using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

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
}
