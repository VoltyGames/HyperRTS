using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How close counts as there, or as clear, shared by planning, the wall constraint and locomotion.</summary>
    public static class NavTolerances
    {
        /// <summary>
        /// Metres of float noise allowed against walls: a cell this short of a unit's radius still counts as open, and
        /// a move may deepen an overlap by this much, so a unit sliding along a wall never wedges.
        /// </summary>
        public const float WallOverlap = 0.01f;

        /// <summary>Distance from the final waypoint at which locomotion has arrived.</summary>
        public static float Arrive(float radius) => math.max(0.05f, radius * 0.25f);
    }
}
