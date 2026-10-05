using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Paid-for units waiting to be trained; the first element is in progress.</summary>
    [InternalBufferCapacity(5)]
    public struct ProductionQueueItem : IBufferElementData
    {
        public Entity Prefab;
    }
}
