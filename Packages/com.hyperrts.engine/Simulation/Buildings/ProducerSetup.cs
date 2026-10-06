using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the production components to a building; fill the returned buffer with unit prefabs.</summary>
    public static class ProducerSetup
    {
        public static DynamicBuffer<ProductionOption> Add<TSink>(ref TSink sink, float3 spawnOffset, int queueLimit)
            where TSink : struct, IComponentSink
        {
            sink.Add(new Producer { SpawnOffset = spawnOffset, QueueLimit = queueLimit, Speed = 1f });
            sink.AddBuffer<ProductionQueueItem>();
            sink.Add<RallyPoint>();
            sink.SetEnabled<RallyPoint>(false);
            return sink.AddBuffer<ProductionOption>();
        }
    }
}
