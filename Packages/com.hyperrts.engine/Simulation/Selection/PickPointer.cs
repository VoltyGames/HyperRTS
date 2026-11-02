using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Where the pointer is on screen and the camera that maps the world to that screen.</summary>
    public readonly struct PickPointer
    {
        public readonly float2 Position;

        /// <summary>Projection * worldToCamera.</summary>
        public readonly float4x4 ViewProjection;

        public readonly float2 ScreenSize;

        public PickPointer(float2 position, float4x4 viewProjection, float2 screenSize)
        {
            Position = position;
            ViewProjection = viewProjection;
            ScreenSize = screenSize;
        }
    }
}
