using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Raises the owner's population cap once the building is complete.</summary>
    public struct PopulationProvider : IComponentData
    {
        public int Value;
    }
}
