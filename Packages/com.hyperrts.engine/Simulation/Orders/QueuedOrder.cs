using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Shift-queued orders, promoted to <see cref="ActiveOrder"/> one at a time.</summary>
    [InternalBufferCapacity(4)]
    public struct QueuedOrder : IBufferElementData
    {
        public Order Value;
    }
}
