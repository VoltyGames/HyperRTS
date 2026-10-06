using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Interaction
{
    /// <summary>Client singleton: a ground point the HUD (minimap) asks the camera to centre on; the camera clears it.</summary>
    public struct CameraFocusRequest : IComponentData
    {
        public bool Pending;
        public float3 Point;
    }
}
