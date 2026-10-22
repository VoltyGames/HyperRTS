using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>How close counts as there, shared by locomotion and its progress measure.</summary>
    public static class NavTolerances
    {
        /// <summary>Distance from the final waypoint at which locomotion has arrived.</summary>
        public static float Arrive(float radius) => math.max(0.05f, radius * 0.25f);
    }
}
