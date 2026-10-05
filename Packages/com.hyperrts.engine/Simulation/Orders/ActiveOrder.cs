using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>The order a unit is executing; disabled means idle. The behaviour that finishes it disables it.</summary>
    public struct ActiveOrder : IComponentData, IEnableableComponent
    {
        public Order Value;
    }
}
