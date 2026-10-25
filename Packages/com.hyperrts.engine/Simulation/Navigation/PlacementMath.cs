using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>The default facing shared by placed buildings, start items and spawned sides.</summary>
    public static class PlacementMath
    {
        /// <summary>Yaw in radians that points a front (+Z) at the match camera, which looks along +Z.</summary>
        public const float CameraYaw = math.PI;
    }
}
