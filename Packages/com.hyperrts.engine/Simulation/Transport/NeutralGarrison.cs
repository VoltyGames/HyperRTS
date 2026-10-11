using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>A neutral building anyone may garrison: it belongs to its passengers while occupied, neutral when empty.</summary>
    public struct NeutralGarrison : IComponentData
    {
    }
}
