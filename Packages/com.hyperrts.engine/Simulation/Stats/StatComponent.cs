using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A component with stat fields and its run of <see cref="StatFieldTable.Fields"/>.</summary>
    public struct StatComponent
    {
        public ComponentType Component;

        /// <summary>The component's size in floats.</summary>
        public int Floats;

        public int First;
        public int Count;
    }
}
