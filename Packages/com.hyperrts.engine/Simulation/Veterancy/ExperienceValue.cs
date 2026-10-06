using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Experience the killer earns when this entity dies.</summary>
    public struct ExperienceValue : IComponentData
    {
        public float Value;
    }
}
