using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Sight radius revealing fog of war for the owner's team.</summary>
    public struct VisionRange : IComponentData
    {
        [GhostField] public float Value;
    }
}
