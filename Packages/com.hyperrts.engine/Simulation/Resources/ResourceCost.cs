using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Price of producing or placing a prefab.</summary>
    [InternalBufferCapacity(2)]
    public struct ResourceCost : IBufferElementData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;

        /// <summary>Every resource of the cost summed, e.g. to weigh a unit's worth.</summary>
        public static int Total(DynamicBuffer<ResourceCost> cost)
        {
            var total = 0;
            foreach (var item in cost)
            {
                total += item.Amount;
            }

            return total;
        }
    }
}
