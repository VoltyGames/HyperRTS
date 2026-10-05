using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Price of producing or placing a prefab.</summary>
    [InternalBufferCapacity(2)]
    public struct ResourceCost : IBufferElementData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
    }
}
