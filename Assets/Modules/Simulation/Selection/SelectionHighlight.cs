using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Linear RGBA highlight colours.</summary>
    public struct SelectionHighlightColors : IComponentData
    {
        public float4 Selected;
        public float4 Deselected;
    }
}
