using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How one chosen request is served.</summary>
    internal enum PathTaskKind : byte
    {
        /// <summary>No search: straight, or stay.</summary>
        Direct = 0,

        /// <summary>The fine A* in the corridor of its abstract path.</summary>
        Search = 1,

        /// <summary>A trace down a group's flow field.</summary>
        Trace = 2,
    }

    /// <summary>One request chosen this frame and how to serve it.</summary>
    internal struct PathTask
    {
        /// <summary>Index into the frame's sorted requests.</summary>
        public int Request;

        public PathTaskKind Kind;
        public PathQuery Query;

        /// <summary>The group field a trace follows.</summary>
        public int Field;

        /// <summary>The abstract path's sectors in <see cref="PathPlan.Sectors"/>.</summary>
        public int FirstSector;

        public int SectorCount;
    }

    /// <summary>The frame's chosen tasks, the abstract paths they search along and the group fields to build.</summary>
    internal struct PathPlan
    {
        public NativeList<PathTask> Tasks;
        public NativeList<int> Sectors;

        /// <summary>Indices of new fields in <see cref="GroupFields.Fields"/>.</summary>
        public NativeList<int> Builds;

        /// <summary>Per task: cells expanded, and 1 when it ran a fine search.</summary>
        public NativeList<int2> Results;

        public PathPlan(Allocator allocator)
        {
            Tasks = new NativeList<PathTask>(64, allocator);
            Sectors = new NativeList<int>(256, allocator);
            Builds = new NativeList<int>(4, allocator);
            Results = new NativeList<int2>(64, allocator);
        }

        public void Dispose(JobHandle dependency)
        {
            Tasks.Dispose(dependency);
            Sectors.Dispose(dependency);
            Builds.Dispose(dependency);
            Results.Dispose(dependency);
        }
    }
}
