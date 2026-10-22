using HyperRTS.Simulation.Navigation;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>The order a unit is executing; disabled means idle. The behaviour that finishes it disables it.</summary>
    public struct ActiveOrder : IComponentData, IEnableableComponent
    {
        public Order Value;

        /// <summary>Ends the order and halts the walk toward its target.</summary>
        public static void Finish(EnabledRefRW<ActiveOrder> busy, EnabledRefRW<MoveDestination> moving)
        {
            busy.ValueRW = false;
            moving.ValueRW = false;
        }

        /// <summary>True until the walk reaches its target; a stuck walk also finishes the order.</summary>
        public static bool StopsShort(WalkUp walk, EnabledRefRW<ActiveOrder> busy, EnabledRefRW<MoveDestination> moving)
        {
            if (walk == WalkUp.Stuck)
            {
                Finish(busy, moving);
            }

            return walk != WalkUp.Reached;
        }
    }

    /// <summary>Shift-queued orders, promoted to <see cref="ActiveOrder"/> one at a time.</summary>
    [InternalBufferCapacity(4)]
    public struct QueuedOrder : IBufferElementData
    {
        public Order Value;
    }

    /// <summary>
    /// Enabled once a move order has pointed locomotion at its goal; disabled whenever the order changes or combat
    /// takes over, so the goal is re-applied afterwards.
    /// </summary>
    public struct MoveOrderState : IComponentData, IEnableableComponent
    {
    }
}
