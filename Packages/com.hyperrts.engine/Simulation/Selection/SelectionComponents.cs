using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Can be picked by click or drag-box; paired with an initially disabled <see cref="Selected"/>.</summary>
    public struct Selectable : IComponentData { }

    /// <summary>Selection membership as an enabled bit, so toggling causes no structural change.</summary>
    public struct Selected : IComponentData, IEnableableComponent { }

    /// <summary>Control-group membership (bit n = group n); added the first time an entity is assigned.</summary>
    public struct ControlGroup : IComponentData
    {
        public byte Mask;
    }
}
