using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Core.Selection
{
    public enum SelectionCommand : byte
    {
        None = 0,
        Click = 1,
        DragRelease = 2,
        DoubleClick = 3,
    }

    /// <summary>
    /// Singleton bridge: <see cref="SelectionInputSystem"/> (managed) writes the gesture, ray and
    /// view-projection; <see cref="SelectionSystem"/> (Burst) reads them. Keeps camera/mouse out of the
    /// hot path and makes selection testable by injecting this directly.
    /// </summary>
    public struct SelectionInput : IComponentData
    {
        public SelectionCommand Command;
        public bool Additive;   // Shift
        public bool Subtract;   // Ctrl

        // Click / double-click ray (world space).
        public float3 RayOrigin;
        public float3 RayDirection;
        public float RayDistance;

        // Drag-box in screen pixels (bottom-left origin); valid when Command == DragRelease.
        public float2 DragMin;
        public float2 DragMax;

        public float4x4 ViewProjection; // projection * worldToCamera
        public float2 ScreenSize;
    }
}
