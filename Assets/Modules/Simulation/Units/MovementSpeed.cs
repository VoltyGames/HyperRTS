using Unity.Entities;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Top speed in world units per second.</summary>
    public struct MovementSpeed : IComponentData
    {
        public float Value;
    }
}
