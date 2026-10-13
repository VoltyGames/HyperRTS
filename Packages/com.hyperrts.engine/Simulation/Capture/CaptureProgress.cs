using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Capture under way: who is capturing and how far (0..1); resets when another player starts.</summary>
    public struct CaptureProgress : IComponentData
    {
        [GhostField] public byte Faction;
        [GhostField(Quantization = 1000)] public float Value;
    }
}
