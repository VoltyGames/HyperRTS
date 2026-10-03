using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Selection membership as an enabled bit, so toggling causes no structural change.</summary>
    public struct Selected : IComponentData, IEnableableComponent { }
}
