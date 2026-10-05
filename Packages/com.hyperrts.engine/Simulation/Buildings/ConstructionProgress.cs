using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Build progress, 0..1. Enabled while under construction; the building is inactive until done.</summary>
    public struct ConstructionProgress : IComponentData, IEnableableComponent
    {
        public float Value;
    }
}
