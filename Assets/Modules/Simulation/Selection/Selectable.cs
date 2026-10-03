using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Can be picked by click or drag-box; paired with an initially disabled <see cref="Selected"/>.</summary>
    public struct Selectable : IComponentData { }
}
