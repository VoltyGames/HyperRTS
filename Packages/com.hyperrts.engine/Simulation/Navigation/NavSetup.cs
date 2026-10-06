using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Adds the components map features give the nav grid: areas, obstacles and the terrain heightfield.</summary>
    public static class NavSetup
    {
        public static void AddArea<TWriter>(ref TWriter writer, float2 size, NavAreaKind kind)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new NavArea { Size = size, Kind = kind });

        public static void AddObstacle<TWriter>(ref TWriter writer, float2 size)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new NavObstacle { Size = size });

        /// <summary>One per match; the caller owns the heightfield's blob.</summary>
        public static void AddTerrain<TWriter>(ref TWriter writer, in TerrainHeight height)
            where TWriter : struct, IEntityWriter =>
            writer.Add(height);
    }
}
