using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Sight radius revealing fog of war for the owner's team.</summary>
    public struct VisionRange : IComponentData
    {
        public float Value;
    }
}
