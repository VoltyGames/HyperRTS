using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Ground points along a camera's view rays, shared by the camera, the minimap and the audio listener.</summary>
    public static class ViewGround
    {
        /// <summary>Where the ray meets the ground, else the ground below its origin (a ray at or above the horizon).</summary>
        public static float3 Focus(in TerrainHeight terrain, float3 origin, float3 direction, float maxDistance) =>
            terrain.Raycast(origin, direction, maxDistance, out var ground) ? ground : OnGround(terrain, origin);

        /// <summary>Where the ray meets the ground, else the ground under its far end, so rays above the horizon reach out.</summary>
        public static float3 Reach(in TerrainHeight terrain, float3 origin, float3 direction, float maxDistance) =>
            terrain.Raycast(origin, direction, maxDistance, out var ground)
                ? ground
                : OnGround(terrain, origin + direction * maxDistance);

        private static float3 OnGround(in TerrainHeight terrain, float3 point) =>
            new(point.x, terrain.Height(point.xz), point.z);
    }
}
