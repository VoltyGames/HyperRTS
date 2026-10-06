using Unity.Entities;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Capture under way: who is capturing and how far (0..1); resets when another player starts.</summary>
    public struct CaptureProgress : IComponentData
    {
        public byte Faction;
        public float Value;
    }
}
