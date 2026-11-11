using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Buildings
{
    /// <summary>Build progress, 0..1. Enabled while under construction; the building is inactive until done.</summary>
    [GhostEnabledBit]
    public struct ConstructionProgress : IComponentData, IEnableableComponent
    {
        [GhostField(Quantization = 1000)] public float Value;
    }
}
