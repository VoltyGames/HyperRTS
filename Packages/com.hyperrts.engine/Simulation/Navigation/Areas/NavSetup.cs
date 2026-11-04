using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Areas
{
    /// <summary>Adds the components map features give the nav grid: areas, obstacles and the terrain heightfield.</summary>
    public static class NavSetup
    {
        public static void AddArea(EntityWriter writer, float2 size, NavAreaKind kind) =>
            writer.Add(new NavArea { Size = size, Kind = kind });

        public static void AddObstacle(EntityWriter writer, float2 size, bool open = false) =>
            writer.Add(new NavObstacle { Size = size, Open = open });

        /// <summary>One per match; the caller owns the heightfield's blob.</summary>
        public static void AddTerrain(EntityWriter writer, in TerrainHeight height) =>
            writer.Add(height);
    }
}
