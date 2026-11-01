using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Counts simulation frames from zero; <see cref="GameEventSystem"/> advances it once at the start of each.</summary>
    public struct SimulationFrame : IComponentData
    {
        public uint Value;
    }
}
