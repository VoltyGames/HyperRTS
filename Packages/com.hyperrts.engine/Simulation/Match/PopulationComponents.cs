using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Per-player supply: units use it, buildings with <see cref="PopulationProvider"/> raise the cap.</summary>
    public struct Population : IComponentData
    {
        [GhostField] public int Used;
        [GhostField] public int Cap;

        public readonly bool HasRoomFor(int amount) => amount <= 0 || Used + amount <= Cap;
    }

    /// <summary>Raises the owner's population cap once the building is complete.</summary>
    public struct PopulationProvider : IComponentData
    {
        public int Value;
    }
}
