using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Live drag-box state in screen pixels (bottom-left origin). Singleton written by the
    /// input layer (<c>SelectionInputSystem</c>) and read by the presentation marquee
    /// (<c>SelectionDragBoxUI</c>) — the ECS hand-off that keeps input and presentation decoupled.</summary>
    public struct SelectionDragState : IComponentData
    {
        public bool IsDragging;
        public float2 StartScreen;
        public float2 CurrentScreen;
    }
}
