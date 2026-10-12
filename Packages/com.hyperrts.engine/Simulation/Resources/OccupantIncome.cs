using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Makes <see cref="ResourceIncome"/> pay its amount once per passenger aboard; empty, it pays nothing.</summary>
    public struct OccupantIncome : IComponentData { }
}
