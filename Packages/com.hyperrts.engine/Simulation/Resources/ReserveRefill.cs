using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// While owned and not <c>Offline</c>, adds <see cref="Amount"/> every interval to each same-owner
    /// <see cref="ResourceReserve"/> whose income pays <see cref="Type"/>, up to its capacity.
    /// </summary>
    public struct ReserveRefill : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public float Interval;
        public float Elapsed;
    }
}
