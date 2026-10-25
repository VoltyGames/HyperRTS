namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Running totals of the pathfinder's work, read by tests and benchmarks.</summary>
    public struct PathStats
    {
        /// <summary>Fine corridor searches run.</summary>
        public int Searches;

        /// <summary>Paths traced down a group's flow field.</summary>
        public int Traces;

        /// <summary>Group flow fields built.</summary>
        public int Fields;

        /// <summary>Cells the fine searches expanded.</summary>
        public long Expansions;
    }
}
