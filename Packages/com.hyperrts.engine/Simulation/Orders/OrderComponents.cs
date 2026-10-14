using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

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
    }

    /// <summary>Shift-queued orders, promoted to <see cref="ActiveOrder"/> one at a time.</summary>
    [InternalBufferCapacity(4)]
    public struct QueuedOrder : IBufferElementData
    {
        public Order Value;
    }

    /// <summary>
    /// Progress of a Move/AttackMove order. Enabled once the order has pointed locomotion at its goal; disabled
    /// whenever the order changes or combat takes over, so the goal is re-applied afterwards.
    /// </summary>
    public struct MoveOrderState : IComponentData, IEnableableComponent
    {
        /// <summary>Where the stall timer last restarted; the unit counts as stalled until it leaves this spot.</summary>
        public float3 StallAnchor;

        public float StallTime;
    }
}
