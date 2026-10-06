using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Paid-for units waiting to be trained; the first element is in progress.</summary>
    [InternalBufferCapacity(5)]
    public struct ProductionQueueItem : IBufferElementData
    {
        /// <summary>Not replicated; clients resolve it from <see cref="TypeId"/>.</summary>
        [GhostField(SendData = false)] public Entity Prefab;

        /// <summary>The prefab's <c>EntityInfo.TypeId</c>.</summary>
        [GhostField] public int TypeId;
    }
}
