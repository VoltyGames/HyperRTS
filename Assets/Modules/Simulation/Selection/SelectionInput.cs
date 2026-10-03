using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    public enum SelectionCommand : byte
    {
        None = 0,
        Click = 1,
        DragRelease = 2,
        DoubleClick = 3,
    }

    /// <summary>Selection gesture written by the input layer and read by <see cref="SelectionSystem"/>; tests inject it directly.</summary>
    public struct SelectionInput : IComponentData
    {
        public SelectionCommand Command;
        public bool Additive;   // Shift
        public bool Subtract;   // Ctrl

        // Click / double-click ray (world space).
        public float3 RayOrigin;
        public float3 RayDirection;
        public float RayDistance;

        // Screen pixels, bottom-left; valid for DragRelease.
        public float2 DragMin;
        public float2 DragMax;

        public float4x4 ViewProjection; // projection * worldToCamera
        public float2 ScreenSize;
    }
}
