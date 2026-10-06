using Unity.Entities;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>A building other players can take over.</summary>
    public struct Capturable : IComponentData
    {
        public float CaptureTime;
    }
}
