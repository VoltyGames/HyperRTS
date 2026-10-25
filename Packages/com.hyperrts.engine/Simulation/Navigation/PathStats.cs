using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Running totals of the pathfinder's work, read by tests and benchmarks.</summary>
    public struct PathStats
    {
        /// <summary>Fine corridor searches run.</summary>
        public int Searches;

        /// <summary>Fine searches per <see cref="PathCause"/>, indexed by its value.</summary>
        public int4 SearchesByCause;

        /// <summary>Paths traced down a group's flow field.</summary>
        public int Traces;

        /// <summary>Traces the field no longer led along, which searched alone and count under no cause.</summary>
        public int Retraces;

        /// <summary>Group flow fields built.</summary>
        public int Fields;

        /// <summary>Cells the fine searches expanded.</summary>
        public long Expansions;

        /// <summary>The planner's estimate of the fine searches' expansions, to check against <see cref="Expansions"/>.</summary>
        public long Estimated;
    }
}
