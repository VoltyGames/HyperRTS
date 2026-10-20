using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// A right-click rule a game adds to a unit: a Smart command on a target that has <see cref="TargetComponent"/>
    /// and stands in one of <see cref="Relations"/> to the unit gives <see cref="Order"/>. Rules are checked in buffer
    /// order, before the engine's own Smart choices. Build them with <see cref="For{TTarget}"/> in a baker.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct SmartOrderRule : IBufferElementData
    {
        public OrderType Order;

        /// <summary>Traits of a game order; engine orders take the engine's own.</summary>
        public OrderTraits Traits;

        /// <summary>Stable type hash of the component the target must have, so baked rules survive type reordering.</summary>
        public ulong TargetComponent;

        public RelationMask Relations;

        public static SmartOrderRule For<TTarget>(OrderType order, OrderTraits traits, RelationMask relations) => new()
        {
            Order = order,
            Traits = traits,
            TargetComponent = TypeManager.GetTypeInfo<TTarget>().StableTypeHash,
            Relations = relations,
        };
    }
}
