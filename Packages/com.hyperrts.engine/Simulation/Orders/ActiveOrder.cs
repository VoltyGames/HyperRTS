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
    }
}
