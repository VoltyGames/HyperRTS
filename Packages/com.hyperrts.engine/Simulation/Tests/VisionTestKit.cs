using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Fog-of-war helpers on top of <see cref="TestWorld"/>.</summary>
    public static class VisionTestKit
    {
        public static FogOfWar Fog(this TestWorld world)
        {
            world.EntityManager.CompleteAllTrackedJobs();
            return world.Singleton<FogOfWar>();
        }

        public static void DisableFog(this TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            var settings = query.GetSingleton<MapSettings>();
            settings.FogOfWar = false;
            query.SetSingleton(settings);
        }
    }
}
