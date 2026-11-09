namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>Planning state of a unit's path.</summary>
    public enum PathStatus : byte
    {
        /// <summary>No path; the next enabled destination requests one.</summary>
        None = 0,

        /// <summary>Waiting for a pathfinding slot; a unit with no corners yet brakes and turns to its goal meanwhile.</summary>
        Requested = 1,
        Ready = 2,
    }
}
