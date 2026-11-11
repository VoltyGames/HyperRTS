using Unity.Entities;

namespace HyperRTS.Simulation.Common.Systems
{
    /// <summary>
    /// Spreads per-entity scans over frames: each entity is due on one frame in <see cref="Interval"/>, the same for
    /// every system, so a unit's scans line up.
    /// </summary>
    public readonly struct ScanSchedule
    {
        /// <summary>Each entity scans every Nth frame.</summary>
        public const int Interval = 4;

        private readonly int _current;

        public ScanSchedule(SimulationFrame frame) => _current = (int)(frame.Value % Interval);

        public bool IsDue(Entity entity) => entity.Index % Interval == _current;
    }
}
