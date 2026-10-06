using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Per-player supply: units use it, buildings with <see cref="PopulationProvider"/> raise the cap.</summary>
    public struct Population : IComponentData
    {
        [GhostField] public int Used;
        [GhostField] public int Cap;

        public readonly bool HasRoomFor(int amount) => amount <= 0 || Used + amount <= Cap;
    }
}
