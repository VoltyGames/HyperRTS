using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Adds the production components to a building; fill the returned buffer with unit prefabs.</summary>
    public static class ProducerSetup
    {
        public static DynamicBuffer<ProductionOption> Add<TWriter>(ref TWriter writer, float3 spawnOffset,
            int queueLimit) where TWriter : struct, IEntityWriter
        {
            writer.Add(new Producer { SpawnOffset = spawnOffset, QueueLimit = queueLimit, Speed = 1f });
            writer.AddBuffer<ProductionQueueItem>();
            writer.Add<RallyPoint>();
            writer.SetEnabled<RallyPoint>(false);
            return writer.AddBuffer<ProductionOption>();
        }
    }
}
