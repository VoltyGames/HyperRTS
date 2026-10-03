using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public enum PathStatus : byte
    {
        /// <summary>No path; the next enabled destination requests one.</summary>
        None = 0,

        /// <summary>Waiting for a pathfinding slot; the unit steers straight at its destination meanwhile.</summary>
        Requested = 1,
        Ready = 2,
    }

    /// <summary>Which destination and grid version a unit's <see cref="PathWaypoint"/>s were planned for.</summary>
    public struct PathState : IComponentData
    {
        public float3 Goal;
        public int GridVersion;
        public PathStatus Status;

        /// <summary>Frame of the pending request; older requests are served first.</summary>
        public uint RequestFrame;
    }
}
