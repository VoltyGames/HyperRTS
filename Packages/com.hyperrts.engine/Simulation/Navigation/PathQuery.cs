using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How a path query is answered.</summary>
    public enum PathRoute : byte
    {
        /// <summary>No ground the start's region reaches lies near the goal, so the unit stays.</summary>
        Stay = 0,

        /// <summary>The end is in sight, or the start has no open ground near it: one straight leg.</summary>
        Straight = 1,

        /// <summary>A corridor search, or a trace of the group's flow field.</summary>
        Search = 2,
    }

    /// <summary>
    /// A start and goal snapped for one movement class by <see cref="GridPathfinder.Prepare"/>: the open cells to
    /// search between and the points the path runs from and to.
    /// </summary>
    public struct PathQuery
    {
        public PathRoute Route;
        public int2 From;
        public int2 To;

        /// <summary>Where the path starts: the unit, or the open cell it first steps out to.</summary>
        public float3 Origin;

        /// <summary>Where the path ends: the goal, or the cell it snapped to.</summary>
        public float3 End;

        /// <summary>The unit stands too near a wall (a building placed on it), so its first waypoint is the origin.</summary>
        public bool StepsOut;

        /// <summary>The path ends at the goal, not snapped.</summary>
        public bool Exact;

        /// <summary>Starts the points: the step out to open ground, if any.</summary>
        public readonly void Begin(NativeList<float3> points)
        {
            points.Clear();
            if (StepsOut)
            {
                points.Add(Origin);
            }
        }

        /// <summary>The whole path of a query that needs no search.</summary>
        public readonly void AddDirect(NativeList<float3> points)
        {
            Begin(points);
            if (Route == PathRoute.Straight)
            {
                points.Add(End);
            }
            else if (!StepsOut)
            {
                points.Add(Origin);
            }
        }
    }
}
