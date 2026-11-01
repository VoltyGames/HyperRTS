using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Counts simulation frames from zero; <see cref="GameEventSystem"/> advances it once at the start of each.</summary>
    public struct SimulationFrame : IComponentData
    {
        public uint Value;
    }

    /// <summary>
    /// Spreads per-entity scans over frames: each entity is owned by one frame in <see cref="Interval"/>, the same for
    /// every system, so a unit's scans line up.
    /// </summary>
    public readonly struct ScanSlot
    {
        /// <summary>Each entity scans every Nth frame.</summary>
        public const int Interval = 4;

        private readonly int _current;

        public ScanSlot(SimulationFrame frame) => _current = (int)(frame.Value % Interval);

        public bool Owns(Entity entity) => entity.Index % Interval == _current;
    }
}
