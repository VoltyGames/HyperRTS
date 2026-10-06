using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How close counts as there, shared by locomotion and the orders that drive it.</summary>
    public static class NavTolerances
    {
        /// <summary>Distance from the final waypoint at which locomotion has arrived.</summary>
        public static float Arrive(float radius) => math.max(0.05f, radius * 0.25f);

        /// <summary>
        /// Near enough for a unit the crowd keeps from its goal: a stalled move counts as done, and a corner may be
        /// skipped once the next one is in sight.
        /// </summary>
        public static float Crowded(float radius) => radius * 4f + 1f;
    }
}
