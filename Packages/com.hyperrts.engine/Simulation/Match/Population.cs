using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Per-player supply: units use it, buildings with <see cref="PopulationProvider"/> raise the cap.</summary>
    public struct Population : IComponentData
    {
        public int Used;
        public int Cap;

        public readonly bool HasRoomFor(int amount) => amount <= 0 || Used + amount <= Cap;
    }
}
