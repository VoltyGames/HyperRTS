using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Detection radius stamped into <see cref="FogOfWar.Detected"/> for the owner's team.</summary>
    public struct Detector : IComponentData
    {
        [GhostField] public float Radius;
    }
}
