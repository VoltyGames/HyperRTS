using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Build progress, 0..1. Enabled while under construction; the building is inactive until done.</summary>
    [GhostEnabledBit]
    public struct ConstructionProgress : IComponentData, IEnableableComponent
    {
        [GhostField] public float Value;
    }
}
