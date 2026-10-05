using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Drag-box state in screen pixels (bottom-left). Written by input, read by the marquee UI.</summary>
    public struct SelectionDragState : IComponentData
    {
        public bool IsDragging;
        public float2 StartScreen;
        public float2 CurrentScreen;
    }
}
