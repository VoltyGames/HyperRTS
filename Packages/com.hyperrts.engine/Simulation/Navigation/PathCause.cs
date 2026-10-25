namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Why a unit's path is planned, counted per fine search in <see cref="PathStats"/>.</summary>
    public enum PathCause : byte
    {
        /// <summary>A new move, or an order that moved the goal.</summary>
        Order = 0,

        /// <summary>A goal that follows a target (<see cref="MoveDestination.Chase"/>) moved off the path's end.</summary>
        Chase = 1,

        /// <summary>The grid changed near the path.</summary>
        GridChange = 2,

        /// <summary>The unit stopped getting on (<c>MoveProgress</c>) and asked for a new path.</summary>
        Replan = 3,
    }
}
