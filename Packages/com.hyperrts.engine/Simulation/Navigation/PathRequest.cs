using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>One pending path search, snapshotted so the search job needs no component reads.</summary>
    internal struct PathRequest
    {
        public Entity Entity;
        public float3 Start;

        /// <summary>The unit's facing on XZ, where the drive planner starts it.</summary>
        public float2 Facing;

        public float3 Goal;
        public NavAgent Agent;

        /// <summary><see cref="MoveDestination.Group"/>: members of one share a flow field.</summary>
        public int Group;

        public uint Frame;

        /// <summary>Why the unit asked, for <see cref="PathStats.SearchesByCause"/>.</summary>
        public PathCause Cause;

        /// <summary>Walls held the unit when it asked, so the drive planner plans its way out from the start.</summary>
        public bool Stuck;
    }
}
